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
`[stepback]` `[hud]` `[error]` `[log]`. Search the file for a tag to follow one area.

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
  - `[mcm] settings page registered at main menu (attempt 1): MCM 5.…, page "Trax Combat Enhancements", 52 settings in 9 groups, …, Default preset = the mod's defaults (defaults.json); group "Defaults": buttons "Revert all to defaults" and "Save current values as a defaults file".`
    (`at retry (attempt N)` is fine too — MCM was just slow to wake)
  - `[config] defaults: defaults.json (embedded in TraxCombat.Core.dll), 52 keys for 52 settings - every default read from it`
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
  back (delete a key's line, delete the file, or MCM's *Revert all to defaults*), then 9
  sections (Master switch, Damage randomness, Athletics, Tired fighters, Tired fighters step
  back, Regeneration, Bars - you and your target, Bars - your squads, Advanced) and above EVERY key a `//` explanation in
  plain words ending `(default …, range … to …)`. The defaults are the ones in the mod's
  `defaults.json` (section 5).
- Log, first run only: `[config] first run: created config.json with every default and a plain-words explanation beside each value`
- Log, every start: `[config] settings in effect (52, version 0):` followed by 52 lines like
  `[config]   DamageRandomPercent = 50` — a value you changed shows `(default 50)` after it.

**1d. The MCM page.** Main menu → Options → Mod Options → *Trax Combat Enhancements*.
- You see: the same 9 groups, 52 settings — *Master switch* (Mod enabled) on top — checkboxes
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
  - `[mission] attached: AthleticsLogic (its HUD views join the mission screen on its first tick - [hud] attached: lines)` (section 7 has the `[hud]` lines)
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
- his attack RATE — wind-up, strike, bow draw, crossbow reload, throws, and for the AI the pause
  between attacks — down to 20% at empty: one attack where he used to make five (3n),
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
  at empty (~16) a wind-up and strike take about five times as long - hammering the button
  gives about a fifth of your fresh rate. Try to run: at empty you run at about a third of
  your normal pace. Blocking is normal (never slowed).
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
    eyes and the summary's **attack rate** lines (3n) and **run speed check** (3m) settle.
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
  — N and M close together = both signals agree; and the `attack rate, ranged, you` rows (3n):
  `draw` and `reload` about x5 at empty (`aim` is your own choice).

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
- *Tired fighters* → *Tired AI attack less often* / *Tired AI keep a slower pace* (the attack
  rate's A/B switches, 3n): each logs `[config] AttackRate…: true → false (source: MCM)`; the
  first one also `[rate] AttackRateAiDecisions switched OFF mid-mission at … s: N tired fighters
  get their AI attack values back over the next ticks …`, the second `[rate] AttackRatePaceHold
  switched OFF mid-mission at … s: N held fighters may attack again at once`. For a clean A/B,
  flip them BETWEEN battles (a battle that flips one says so in its summary).

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
[summary] speed updates: 900 recomputes asked (UpdateAgentProperties: fighters 900, horses 0; a change below x0.05 waits; 0 held a tick by the per-tick budget), the decorator applied attack penalties in 1200 recomputes, run penalties in 1200, horse penalties in 0 (the attack timings by f: the "attack rate" lines)
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
- **Attack speed follows the curve in the engine**: the `attack rate` lines after the
  Athletics block (3n — they replaced step 5c's "attack speed check" lines).
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

**3n. Attack rate — the whole cycle slows, not only the swing (step 5e).** Anton's rule: at
attack speed 50% a man who attacked once a second attacks once every two seconds. A tired
fighter's attack speed m (the `attacks x…` of 3b) now drives three things (DESIGN §2):
- **the animations** (always): wind-up, swing, bow draw, crossbow reload, throws ÷ m;
- **the AI's decisions** (*Tired AI attack less often*, `AttackRateAiDecisions`, on): its
  chance to attack, to strike back after a parry and to loose × m, its aim before a shot ÷ m;
- **the pace hold** (*Tired AI keep a slower pace*, `AttackRatePaceHold`, on): after each melee
  swing a tired AI fighter on foot holds his next attack, guard up, until his fresh rhythm ÷ m
  has passed since that swing (never you, never riders; ranged gets the first two only).
Blocking is never slowed: handling, shield speed and every defence value of the AI are left
alone. The one known gap: the recoil after a blocked blow plays at the game's own speed.

Where to see it cleanly:
- **A tired recruit, one on one** — a town's **arena practice fight** (no step backs there, so
  the AI rows are pure). Fight a recruit-level opponent with a shield and let him swing at you
  while you block: his first 2 swings come at the fresh rhythm, then each one later, and when
  he is empty about one swing where he used to make five — between swings he stands, guard up,
  and still blocks. (A custom battle works too: switch *Tired fighters step back* off for the
  cleanest numbers — cycles with a step back in them are left out and counted, see below.)
- **A tired archer** — custom battle, archers against archers at range: volleys slow as the
  bars empty (recruit archers are empty after 5 shots); an empty archer draws, aims and nocks
  at about a fifth of his fresh pace.
- **You** — hammer the attack button 20 s fresh, then 20 s when empty (3b): your swings come
  about five times rarer. Your pause is yours, so your rows are judged only when you attack as
  fast as you can.
- **A/B in one session** (flip BETWEEN battles, the same custom battle each time):
  battle 1 the defaults; battle 2 *Tired AI keep a slower pace* off (the AI's decisions
  alone); battle 3 it on and *Tired AI attack less often* off (the hold alone). Compare the
  `verdict` lines; tell Claude which read closest to ON TARGET.

What you should feel: tired men fight in slow motion - fewer blows, longer gaps, guard up in
between - while fresh men keep their pace. Halfway down the bar (m about 0.6) about 6 attacks
where there were 10; empty (m 0.2) one where there were 5.

Log:
- `[rate] mission start: ON - animations x m always (swing, thrust / bow draw / throw, reload); AI decisions (AttackRateAiDecisions) on: the chance to attack, to riposte and to loose x m, the aim before a shot ÷ m; pace hold (AttackRatePaceHold) on: …; blocking and the recoil after a block: untouched - read live; …`
- once, the first fighter slowed below full strength - every value the techniques touch,
  before → after, each checked:
  `[rate] first slowed fighter this mission: <name> at … s - attacks x0.97 (f 0.96); T1 animations: swing 1.020 → 0.989, thrust/draw …, reload … (each x0.97 as asked); T2 AI decisions (AttackRateAiDecisions on): attack chance 0.144 → 0.140 (x0.97), riposte chance …, shoot chance …, aim before a shot … (x1.03) (chances x0.97, the aim ÷ 0.97 as asked); T3 pace hold (…): …; untouched on purpose: handling (blocking), shield defend speed, the recoil after a block, AIHoldingReady`
  — **`NOT … as asked - tell Claude`** = a value did not take its factor.
- once, the first hold in full:
  `[rate] first pace hold this mission: <name> at … s - attacks x0.93 (f 0.91); fresh cycle 1.40 s (his own, 3 samples) → target 1.51 s from his swing at … s; his next ready expected to take 0.35 s → held 0.40 s (until … s); scripted flags 0 → 2 (NoAttack set: the engine took it)`
  then `[rate] first pace hold ended at … s after 0.40 s - time up; scripted flags now 0; …`.
  **`NoAttack NOT set`** → tell Claude.
- verbose (bucket rate-hold): every hold, its end and every refusal.

Summary (after the Athletics block; numbers made up):
```
[summary] attack rate settings at the end (DESIGN §2 - the whole cycle follows the attack speed m): ON - animations x m always (…); AI decisions (AttackRateAiDecisions) on: …; pace hold (AttackRatePaceHold) on: …; blocking and the recoil after a block: untouched
[summary] attack rate, melee, AI, peak (f 1): wind-up 0.32 + held 0.08, swing 0.52 (clean, hit nothing 0.60), recoil after a block 0.40, pause 0.45 | cycle 1.40 s (n 900), m 1.00 - the fresh reference
[summary] attack rate, melee, AI, f 0.5-1: wind-up 0.41 (x1.28) + held 0.10 (x1.25), swing 0.66 (x1.27) (clean, hit nothing 0.77 (x1.28)), recoil after a block 0.40 (x1.00), pause 0.80 (x1.78) | cycle 1.85 s (n 300), m 0.78 → target 1.80 s: 103% - on target
[summary] attack rate, melee, AI, f below 0.5: … | cycle 3.20 s (n 120), m 0.42 → target 3.33 s: 96% - on target
[summary] attack rate, melee, AI, empty (f 0): wind-up 1.60 (x5.00) + held 0.40 (x5.00), swing 2.60 (x5.00) (…), recoil after a block 0.41 (x1.02), pause 2.40 (x5.33) | cycle 6.60 s (n 40), m 0.20 → target 7.00 s: 94% - on target
[summary] attack rate, melee, AI - verdict: ON TARGET in 3 of 3 tired bands (fresh cycle 1.40 s; f 0.5-1 103% on target, f below 0.5 96% on target, empty (f 0) 94% on target)
[summary] attack rate, melee, you, peak (f 1): … | cycle 0.95 s (n 40), m 1.00 - the fresh reference
[summary] attack rate, melee, you, empty (f 0): … | cycle 4.60 s (n 12), m 0.20 → target 4.75 s: 97% - on target
[summary] attack rate, melee, you - verdict: …
[summary] attack rate, ranged, AI, peak (f 1): draw 0.60 + aim 0.90, loose 0.20, reload 0.70, pause 0.30 | cycle 2.70 s (n 400), m 1.00 - the fresh reference
[summary] attack rate, ranged, AI, empty (f 0): draw 3.00 (x5.00) + aim 1.80 (x2.00), loose 0.20 (x1.00), reload 3.50 (x5.00), pause 0.90 (x3.00) | cycle 9.40 s (n 30), m 0.20 → target 13.50 s: 70% - too fast
[summary] attack rate, ranged, AI - verdict: OFF TARGET in 1 of 3 tired bands (…)
[summary] attack rate, ranged, you: no attacks measured
[summary] attack rate - left out: cycles whose two ends fell in different f bands (melee AI / you, ranged AI / you) 400 / 10, 50 / 0; longer than 4 s ÷ m melee or 12 s ÷ m ranged (a pause, not a fighting rhythm) 300 / 5, 40 / 0; readies that ended in no attack (cancelled, feints) 200 / 8, 10 / 0; chained (the next ready straight out of the last attack, pause 0) 120 / 20, 0 / 0; with a step back in them (its pause, not the attack rhythm) 60 / 0, 0 / 0
[summary] attack rate - AI decisions (AttackRateAiDecisions on at the end): scaled in 1100 recomputes - the chance to attack and to riposte x m, to loose x m, the aim before a shot ÷ m; …
[summary] attack rate - pace hold (AttackRatePaceHold on at the end; tired AI fighters on foot, after a melee swing): 500 holds, avg 0.85 s, max 3.10 s at avg m 0.45; by f: f 0.5-1 200, f below 0.5 200, empty (f 0) 100
[summary] attack rate - pace hold, not held: at full strength 2000, not needed (his swing and next ready already fill the target) 150, the next attack already readied at the swing's end 60, no fresh cycle known yet 5, stepping back 60, you 40, riders 80 | not started by the tick: busy with a game job (…) 3, NoAttack already set by the game 0, …
[summary] attack rate - pace hold ends: time up 480, a swing started anyway 2 (must be about 0 - NoAttack holds swings), switched off 0, left the field 15, mission end 3, you took him 0, mounted 0, error 0 | NoAttack cleared by us 495, already cleared by the game 2, a game job on him at the end (left alone, cleared once free: 1, of them under a long scripted frame: 0) 1, still held at mission end 3 | the next ready came avg 0.15 s after a hold ended (n 420) - near 0 = the hold set his rhythm
[summary] attack rate - guard by f (melee hits on fighters on foot that were blocked or parried; blocking is never slowed - tired men must not block less): peak (f 1) 45% (n 900) | f 0.5-1 47% (n 400) | f below 0.5 46% (n 150) | empty (f 0) 44% (n 60) | while held by the pace hold 52% (n 90)
```
What proves what:
- **The whole cycle follows m**: each group's `verdict` line - `ON TARGET` = every tired band's
  cycle within ±15% of the peak's cycle ÷ m. `too fast` / `too slow` names the band.
- **Which phase did it** (the x beside each phase is its ratio to the peak row): wind-up and
  swing (draw and reload) about 1/m = **the engine honours the animation multipliers** (5c's
  UNVERIFIED #2); `pause` growing = the AI's decisions and / or the hold; `recoil after a block`
  near x1.00 is the known gap (the game's own speed - tell Claude if it matters in play).
- **AI rows too fast with the hold ON**: read `pace hold ends` - `a swing started anyway` well
  above 0 = the engine ignores NoAttack outside a scripted move → tell Claude. `not held … the
  next attack already readied` large = the AI chains its blows before the hold can start.
- **AI rows too fast with the hold OFF (battle 2)** = the AI's decisions alone do not slow its
  pause enough (their native meaning is UNVERIFIED) → keep the hold on.
- **AI rows too slow with both on** = the two stack; battle 3 (hold alone) tells which to keep.
- **Ranged AI** has no hold: `aim` and `pause` growing = the AI's shooting values work;
  `too fast` there → tell Claude (a ranged hold is the next lever).
- **You**: `wind-up` / `swing` about 1/m; the cycle verdict counts only if you attacked as fast
  as you could.
- **Blocking is untouched**: the `guard by f` rows within ~10 points of the peak row, and
  `while held` not below it. Tired men blocking much less → tell Claude.
- `the next ready came avg … after a hold ended` near 0 = the hold set his rhythm (the AI
  attacks the moment it may).

---

## 4. The master switch — the same battle with and without the mod

*Mod enabled* (`ModEnabled`, MCM → *Master switch*, the first key of config.json) turns the
WHOLE mod off, live: no damage rolls, no Athletics costs, no refill, no slow attacks, no
step-backs (anyone stepping back walks back to his formation at once; and, once they exist,
no bars). The log keeps recording, so two battles can be
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
  - if anyone was stepping back (section 6): `[stepback] the whole mod (ModEnabled) switched OFF mid-mission at 42.3 s: N fighters stepping back released to their formations at once`
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
- Log: `[config] defaults: defaults.json (embedded in TraxCombat.Core.dll), 52 keys for 52 settings - every default read from it`
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
  - `[config] reverted all 52 settings to their defaults (defaults.json (embedded in TraxCombat.Core.dll)): 2 changed, applied live`
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

---

## 6. Step back — tired fighters fall back, fresh ones take the blows

After a melee swing, an AI fighter on foot who is below full strength may step back: about
2 m straight away from the man he fights, facing him, at a walk, no swings (guard up), for up to
1.5 s — then his formation takes him back. Chance = 100% × (1 − f): never at full strength,
half the time halfway down, after every swing when empty. Never you, never riders, never after
a shot or a throw. Field battles only (not tournaments, arena, duels, naval battles), not in a
shield wall, square or circle, not on ladders, siege engines or wall edges. MCM group *Tired
fighters step back*. How it is done (and why): AI_NOTES "Step 5d" — the engine's own scripted
movement, the way vanilla sends a soldier to pick up arrows mid-battle.

**6a. See it.** Custom battle, infantry against infantry (e.g. 60 v 60, no archers, a flat
map). Let the lines meet (Charge, or hold and let them come), and after about a minute of
fighting watch the front line from close by (or from the side with the free camera).
- You see: a man who has swung a few times backs off a step or two, still facing the enemy,
  shield up, and walks back into the line a moment later; the men beside him keep fighting.
  It happens more and more as the fight wears on.
- Log:
  - `[stepback] mission start: ON - after a melee swing an AI fighter on foot steps back with chance 100% x (1 - f) (0 at full strength), 2.0 m straight away from his enemy for up to 1.5 s, only with the enemy within 4.0 m, no swings while stepping back, at most 50 at once - read live; technique: a scripted step - …`
  - first tick: `[stepback] this mission allows step backs while it is in battle mode (now: …)`
  - the FIRST step back, in full (always logged):
    `[stepback] first step back this mission: <name> at 63.2 s (f 0.40, Athletics 20.0 of 50, chance 60%, swing ended at 63.1 s) - from (x, y, z) to (x, y, z) (2.00 m straight away from <enemy>, 1.4 m off; asked to face him: 123°); facing before: 12° off his enemy; formation 1 Infantry (Charge, Line, charging); scripted flags none → GoToPosition|NoAttack|… (GoToPosition set: the engine took it)`
  - and its end:
    `[stepback] first step back ended (time up) after 1.5 s: now at (…), moved 1.30 m (0.70 m from the spot), facing his enemy (15° off, 2.6 m from him); mid-step facing his enemy (20° off), moving away (0.90 m/s away); hits taken 1 (blocked 1), swings 0; released: scripted movement off, flags now … - his formation takes him back`
  - with VerboseLogging: every `[stepback] step back: …`, `step back ended (…): …` and
    `step back not started: … - <reason>`.

**6b. Force it.** MCM → *Tired fighters step back* → *Chance when empty (%)* 100 (the default),
and *Athletics* → *Smallest bar (points)* (`AthleticsPoolFloor`) 10 — recruits are then empty
after one or two blows (or raise *Cost per blow*). Fight.
- You see: most tired men step back after every swing — the front line "breathes" back and
  forth, but keeps its shape.
- Summary: the `empty (f 0)` row of the rolls line near `chance avg 100%, dice yes … (100%)`,
  `most at once` near the cap.

**6c. Turn it off mid-battle.** While men are stepping back: Escape → Mod Options → *Tired
fighters step back* off → Done → fight on → back on.
- You see: anyone stepping back walks straight back into his formation; nobody steps back any
  more. Back on: they start again from their next swing.
- Log: `[config] StepBackEnabled: true → false (source: MCM)`, then on the next frame
  `[stepback] settings now: OFF (StepBackEnabled)` and
  `[stepback] StepBackEnabled switched OFF mid-mission at 88.0 s: N fighters stepping back released to their formations at once`;
  back on: `[stepback] StepBackEnabled switched ON mid-mission: tired fighters step back again from their next swing`.
- The same with *Athletics* off (`AthleticsEnabled switched OFF …`) and the master switch (4b).
- Any other step-back setting changed mid-battle: `[stepback] settings now: ON - …` with the new numbers.

**6d. Where it must NOT happen** — look, and read the summary's `not rolled` / `not started`:
- you (`not rolled: you N`), riders (`riders N`), archers shooting (a shot is no swing: never rolled);
- a formation in *Shield wall*, *Square* or *Circle* (`not started … shield wall/square/circle N`);
- a formation ordered to retreat (`formation retreating`), routing men (`routing`);
- sieges: men on ladders, in siege towers, at siege engines (`busy (the game's own check: …)`);
  on the walls a step that would go over the edge or down stairs (`spot not level (wall edge,
  stairs)`, `no straight way back (wall, fence, gap)`, `spot off the navmesh`);
- tournaments and arena fights: `[stepback] this mission is a tournament or arena fight (…) - no step backs here (vanilla AI)`;
  naval battles: `… is a naval battle (moving decks) …`; both count as
  `not a field battle (tournament, arena, duel, naval, deployment, ending) N`.
- Note: a hideout boss fights you in battle mode — a tired boss may step back from you. Say if
  that feels wrong.

**6e. The summary** — 8 lines, `[summary] step back …`:
```
step back - technique: a scripted step - …; settings at the end: ON - …
step back rolls after AI melee swings on foot, by f (the chance must be 0% at full strength and rise as f falls): peak (f 1) 900 swings, chance avg 0%, dice yes 0 (0%) | f 0.5-1 700 swings, chance avg 27%, dice yes 190 (27%) | f below 0.5 400 swings, chance avg 70%, dice yes 280 (70%) | empty (f 0) 150 swings, chance avg 100%, dice yes 150 (100%); not rolled: you 40, riders 30, not a field battle (…) 0
step back starts: dice yes 620 → started 480 (holding a line 200, charging 270, no formation 10), most at once 35; not started 140 (…reasons…)
step back ends: 480 - completed (time up) 430, cut short 50 (left the field 30, formation order changed 15, …)
step back moves: avg 1.20 m of 2.00 asked (min …, max …, reached the spot … of 480), lasted avg 1.40 s (n 480)
step back facing (THE risk: a turned back) - at the start: facing his enemy … | mid-step (… sampled): facing his enemy …, side-on …, back turned …; moving away …, … | at the end (…): …
step back guard: hits taken while stepping back N - blocked B (P%), landed … | everyone else on foot: … hits, blocked … (Q%) | swings started while stepping back 0 (0 expected: StepBackHoldAttacks is on)
step back release check: N released through the engine - scripted movement still on right after 0 (must be 0), our flags (NoAttack, DoNotRun) cleared by hand 0 | at mission end: K were mid-step (released then), overdue (past their time) 0 (must be 0), scripted movement still on after that release 0 (must be 0)
```
- The **rolls** line proves the chance: the `peak (f 1)` row is ALWAYS `chance avg 0%, dice yes 0`,
  and the rows below rise (about 100% × (1 − f)).
- *holding a line* vs *charging* says where they happened (a formation holding its place, or a
  charge melee).

**What counts as broken** — tell Claude and send the log:
1. **Men turn their backs** to walk away (THE risk — the engine decides how a scripted man
   faces). Proof: the `facing` line's **mid-step** part — `back turned` should be near 0,
   `facing his enemy` the big number, `moving away` most of the motion; the first step back's
   `mid-step facing his enemy (N° off)`. If backs turn: set *Chance when empty* to 0 (or the
   step back off) and tell Claude — the fallback is "hang back" (tired men hold their spot
   instead of pressing, through the AI's own behaviour values; no scripted movement).
2. **Men get stuck** (standing behind the line and never coming back, or frozen): the
   `release check` line — every `(must be 0)` number must be 0; the first step back's
   `released: scripted movement off`. Anything else → tell Claude.
3. **Formations fall apart** (the line loses its shape, men drift away): fight the same battle
   with the step back off and compare; `most at once` shows how many were out at the same time —
   lower *Most at once* (`StepBackMaxAtOnce`, 50) to 10-20 if the line gets too loose.
4. **They drop their guard**: the `guard` line — `blocked B (P%)` while stepping back well below
   everyone else's `(Q%)` means the step (with no swings) costs them their guard → try *No
   swings while stepping back* off (`StepBackHoldAttacks`) and compare.
5. `swings started while stepping back` well above 0 with *No swings…* on: the engine ignores
   our NoAttack flag.
6. `not started … engine did not take the scripted position N` large: the engine refuses the call.
7. `step back moves: avg X m` — a tired man walks slowly, so 0.8-1.5 m of 2 is normal; about 0
   means nobody actually moves.

---

## 7. Your Athletics bar

The mod's first thing on screen (step 6). **Where:** bottom right, just under the vanilla
health bar (and under the horse bar when you ride), its right end lined up with the health
bar's fill. **What:** one row — the word *Athletics* (grey), the number `132 / 180` (Athletics
left / your pool — your Athletics skill, at least 50), and a slim bar (205 × 12 UI pixels, as
long as the inside of the health bar). Inside the bar:
- the **fill** from the left, coloured by f (the share of the peak line you have left):
  **green** at or above the line (full strength), **blue** just below it, **yellow** at or below
  75% of the line (about 56% of the bar), **orange** at or below 50% (38%), **red** at or below
  25% (19%);
- a thin **white marker** at 75% of the bar — the peak line (`AthleticsPeakPercent`); at or
  above it you fight at full strength;
- when you are wounded, the right end of the bar is **dark red-brown** — the part your wounds
  hold (at 62% health the last 38%). The fill never reaches into it; refill stops at its edge;
- the rest, the part you can still refill, is **dark grey**;
- **empty**: no fill, the word turns to *Exhausted*, and the word, the number and the bar's thin
  frame turn **red**.

A picture in words (fresh, then tired and wounded):
```
                                     [♥ ████████████ health ████████████ ]
             Athletics  180 / 180   [██████████████████████████|███████]     all green, marker at 3/4
             Athletics   71 / 180   [██████████░░░░░░░░░░░░░░░░|░▓▓▓▓▓▓]     yellow fill, grey, dark wounded end
             Exhausted    0 / 180   [░░░░░░░░░░░░░░░░░░░░░░░░░░|░▓▓▓▓▓▓]     all red text and frame
```
The number is rounded UP: it reads 0 only when the bar is really empty. It updates 10 times a
second (`HudRefreshSeconds`). The game's own UI scale makes the whole row bigger or smaller, as
it does the health bar. The bar never takes the mouse (clicks go through it).

When it shows: in a fight (battle, duel, tournament, and stealth missions — you pay Athletics
there too), with you on the field, while *Mod enabled*, *Athletics* and *Your Athletics bar*
(`ShowPlayerBar`) are on, and not while the game's *Hide Battle UI* (Options → Gameplay) or
photo mode is on. Not during deployment, conversations or cutscenes; gone when you are knocked
out or killed.

**7a. See it.** Any battle (a custom battle is quickest). During deployment nothing; the moment
the fight starts with you on the field, the row appears, green, the number at your pool.
- Log:
  - first tick: `[hud] attached: player bar (PlayerAthleticsView, movie TraxPlayerAthleticsBar, prefab installed) - shown while ModEnabled, AthleticsEnabled and ShowPlayerBar are on, the game's Hide battle UI and photo mode are off, in a fight (battle, duel, tournament or stealth mode) and you are on the field`
  - during deployment (or a walk in town): `[hud] player bar: not shown at 0.0 s - not a fight (mission mode) - mode Deployment`
  - the fight starts: `[hud] player bar: layer created at 31.2 s (mode Battle, was hidden: not a fight (mission mode)) - movie TraxPlayerAthleticsBar loaded OK (13 widgets)`
  - `[hud] player bar: first values pushed at 31.2 s - 180 / 180, fill 1.00, usable 1.00, colour green (peak zone - full strength), f 1.00, peak marker at 75% of the bar; bar 205 x 12 px, 62 px from the right edge and 54 px from the bottom (UI pixels - the game's UI scale applies); colours: yellow at or below 75%, orange 50%, red 25% of the peak line (green at or above it, blue just below)`
  - `[hud] player bar: GREEN for the first time this battle at 31.2 s - f 1.00, 180 / 180 (fill 1.00)`
  - the number must match `[athletics] YOU: Athletics skill 180 → pool 180 …` (section 3).

**7b. Swing till the colours change.** Swing at the air (every swing costs, landed or not —
about 5.6 a swing for you as a party leader). To get there fast, raise *Cost per blow* to 30 for
this test, or use a character with a low Athletics skill.
- You see: the number drops at every swing, the fill shrinks; still green down to the marker,
  then blue, yellow, orange, red; at 0 the row turns red and reads *Exhausted*. Stop swinging:
  about 3 s later it refills, the colours climb back, green again past the marker.
- Log — the FIRST time of each colour in a battle (always written):
  - `[hud] player bar: BLUE for the first time this battle at 40.3 s - f 0.97, 131 / 180 (fill 0.73)`
  - `[hud] player bar: YELLOW …`, `ORANGE …`, `RED …` (the f at each must fit: yellow ≤ 0.75,
    orange ≤ 0.50, red ≤ 0.25; blue between 0.75 and 1)
  - `[hud] player bar: EXHAUSTED shown at 58.9 s - 0 / 180: the label reads "Exhausted", label, number and frame red`
  - with VerboseLogging, every later change: `[hud] player bar: colour yellow → orange at 71.4 s (f 0.49, 66 / 180)`

**7c. Get wounded.** Take a few hits (a looter will do).
- You see: at once the right end of the bar goes dark red-brown; the fill cannot pass into it
  and refill stops at its edge. Below about 75% health the bar can never be green again, below
  about 56% not even blue (the peak line stays at 75% of your FULL pool).
- Log: `[hud] player bar: wounded at 44.0 s - the last 38% of the bar shown dark: usable 112 of 180 (health caps the bar); f can reach at most 0.83 now`
- With *Wounds cap the bar* (`HealthCapsAthletics`) off: no dark part, whatever your health.

**7d. Switch it off and on mid-battle.** Escape → Mod Options → *Bars - you and your target* →
*Your Athletics bar* off → Done → back to the fight → on again. (The game is paused while the
menu is open: the bar goes the moment you are back.)
- Log: `[config] ShowPlayerBar: true → false (source: MCM)`, then
  `[hud] player bar: layer removed at 90.4 s - ShowPlayerBar off`; back on:
  `[hud] player bar: layer created at 101.0 s (mode Battle, was hidden: ShowPlayerBar off) - movie TraxPlayerAthleticsBar loaded OK (13 widgets)` and a new `first values pushed` line.
- The same with the master switch *Mod enabled* (`… layer removed at … - ModEnabled off (the master switch)`)
  and with *Athletics* off (`… - AthleticsEnabled off`). Back on, the bar is full (a fresh start).
- Any Advanced layout setting changed mid-battle (*Your bar: length / thickness / from the right
  edge / from the bottom edge*) moves the bar at its next refresh.

**7e. Hide Battle UI, photo mode, death.**
- Options → Gameplay → *Hide Battle UI* on: the bar goes with the rest of the HUD —
  `[hud] player bar: layer removed at … - the game's Hide battle UI is on`; off: `layer created … (… was hidden: the game's Hide battle UI is on)`.
- Photo mode (if you use it): `… layer removed at … - photo mode`.
- Get knocked out: `… layer removed at … - no player agent on the field`.
- A conversation or cutscene inside a mission: `… - not a fight (mission mode) - mode Conversation` (or `CutScene`).

**7f. Where exactly — resolution, UI scale, riding.** Look at the bar at your resolution; change
the game's UI scale once; mount a horse (the horse bar appears between the health bar and ours).
- You see: the row under the health bar (and the horse bar), not overlapping either, not cut off
  at the screen edge; it scales with the UI scale like the health bar.
- If it sits wrong: move it live with the four Advanced settings (*Your bar: …*, UI pixels of the
  1920 × 1080 layout) until it looks right, and tell Claude the four numbers — they become the
  defaults. (This is the one thing no offline check can prove — RESEARCH UNVERIFIED #8.)
- War Sails: while you steer a ship the vanilla hero bar shrinks and drops lower; say whether ours
  then collides with it.

**7g. The summary** — two `[summary] hud:` lines:
```
[summary] hud: player bar (movie TraxPlayerAthleticsBar) - on screen 312.4 s of 340.2 s (92%); layer built 2x, removed 2x (ShowPlayerBar off 1, mission end 1); hidden: ShowPlayerBar off 7.7 s, not a fight 20.1 s; 3120 refreshes; errors 0
[summary] hud: player bar colours on screen - green 180.0 s (58%), blue 60.0 s (19%), yellow 40.0 s (13%), orange 20.0 s (6%), red 12.4 s (4%); 14 colour changes; exhausted shown 2x (8.2 s); wounded part shown 45.0 s (lowest usable 62% - the last 38% of the bar dark)
```
- *on screen … of …* ≈ the time you were on the field in the fight; every *removed* has its
  reason; *refreshes* ≈ 10 a second on screen; *errors 0*.
- A mission without a screen (rare) says `[summary] hud: no views attached (…)`.

**What counts as broken** — tell Claude and send the log:
1. **No bar at all** in a field battle with you on the field. Read the `[hud]` lines in order:
   no `attached:` line → the view never joined (look for `[error] hud.attach`); `prefab NOT FOUND`
   → the GUI folder did not install; `movie … FAILED to load` → the prefab is broken;
   `layer created … loaded OK` but nothing visible → it is drawn off screen (7f) or invisible.
2. **Wrong place or overlapping** the vanilla HUD at your resolution / UI scale (7f).
3. **Colour does not match** the bar: green with the fill below the marker, blue above it, or a
   colour against its `for the first time` line's f.
4. **Number wrong**: not your pool (compare the `[athletics] YOU:` line), counts up while you
   swing, or reads 0 with fill left.
5. **Wounded part missing** after hits (with *Wounds cap the bar* on), or the fill running into it.
6. **Stays on screen** in a conversation, a cutscene, deployment, with Hide Battle UI on, or after
   you are knocked out; or does not come back when those end.
7. Any `[error] hud.…` line, or `[hud] player bar: … DISABLED for the rest of this battle` (the
   battle goes on without the bar — that is the fail safe working, but it is a bug).
8. **The mouse snags** on the bar (e.g. clicking in the orders menu near it does nothing).

## 8. Orders-menu strip

Step 9, your words: "some vision of the state of the troops". **Where:** while the orders menu
is open, right under each of the game's formation cards (the cards at the left and right edges
of the screen). **What:** under a card, on its left `72% ± 8` — the men's average Athletics as a
share of their own bars, ± their spread; on its right `HP 81%` — their average health left. The
middle stays free for the game's own order icons (the order and target icons that hang under a
card). Under those icons, a slim bar as wide as the card: the average fill in the colour of the
men's average f (green at full strength, then blue, yellow, orange, red — the same colours as
your own bar), a lighter band around the fill's end (the ± spread), a thin tick at 75% (the
peak line).
```
   ┌──────────────┐
   │ morale  83   │     the game's card: morale, troops, captain,
   │ ⚔ 40         │     and at its bottom the ammo bar
   │   ───────    │     ("arrows remaining")
   └──────────────┘
   72% ± 8 [⚑◎] HP 81%       ← ours: numbers left and right of the game's order icons
   [██████████▒▒▒│░░░░]      ← ours: fill (colour by f), lighter ± band, tick at the peak line
```
You (the player) are not in the numbers — the card counts the men under your command, and so
do we. The numbers refresh every quarter second of battle time (the battle runs at a quarter
speed while the menu is open, so about once a second in real time).

When it shows: *Mod enabled*, *Athletics* and *Orders menu strip* (`ShowInOrderMenu`) on, in a
fight with you on the field, not with *Hide Battle UI* or photo mode, and only while the orders
menu is open. Not during deployment (the deployment screen has no cards).

**8a. See it.** A battle with several formations (a custom battle: infantry, archers, cavalry).
Hold/press the orders key. Look under each card.
- You see: a cell under every card with men, lined up with the card's left and right edges; no
  cell under an empty slot; nothing covering the game's cards or order icons (our numbers may
  touch the icons' edges, never sit on them).
- Log — the first open of the battle:
  - at the first tick: `[hud] attached: orders strip (OrderStripView, movie TraxOrderStrip, prefab installed) - shown while ModEnabled, AthleticsEnabled and ShowInOrderMenu are on, the game's Hide battle UI and photo mode are off, in a fight (battle, duel, tournament or stealth mode), you are on the field and while the orders menu is open`
  - `[hud] orders strip: layer created at 35.2 s (mode Battle, was hidden: the orders menu is closed) - movie TraxOrderStrip loaded OK (… widgets) - later builds and removals by "the orders menu is closed" go to the verbose log only`
  - **the proof of the alignment**: `[hud] orders strip: first placement under the cards at 35.2 s (open #1) - technique: the game's own cards read live, layer MissionOrder: 16 cards = 16 cards in 2 sets, set 1 drawn (the game's side columns - keyboard layout); screen 1920 x 1080 px, UI scale 1.00; cards drawn: 1 Infantry at (20, 44) 131 x 223, 40 men (= formation) | 2 Archers at (20, 307) …; cells (numbers 13 px at card bottom +1, bar 4 px at +20, 2 px in from the sides (23 px deep; …)): 1 Infantry at (20, 268) w 131 | …`
    — every drawn card should say `(= formation)`; each cell's x = its card's x, its y = the
    card's y + height + 1 (at UI scale 1).
  - `[hud] orders strip: values at 35.2 s (open #1, under the cards): 1 Infantry 72% ± 8 HP 81% (40 men, f 0.93) | 2 Archers … - ± is 1.00 std, health on`
    — compare with what you see; *f* sets the colour (≥ 1 green, above 0.75 blue, ≤ 0.75
    yellow, ≤ 0.50 orange, ≤ 0.25 red).
- Every later open writes only its `values at … (open #N, …)` line (closing and opening again is
  quiet; with VerboseLogging on, each build and removal too).

**8b. Fight, then look again.** Let the infantry fight a while, open the menu.
- You see: the infantry's number and fill lower, the colour moving down the scale, the band
  wider if some men are fresh and some spent; `HP` dropping as they take wounds. A formation
  that has not fought stays green at 100%.
- Log: the new `values at …` line; at battle end the Athletics block's line
  `Athletics your formations at the end: 1 Infantry 61 ± 14 (38 men) f avg 0.71, 9 at full strength, health avg 74% | …`.

**8c. With RTS Camera Command System on, and off.** Play one battle with it on (as you usually
do) and one with it off.
- You see: the same strip under the cards both times. With RTS Camera the columns run bottom to
  top (Infantry is the bottom-left card) and its card always shows the current-order icon under
  it — our numbers sit left and right of it.
- Log, with RTS Camera: `… layer MissionOrder: 8 cards = 8 cards in 1 set, set 1 drawn (one layout - an order-menu mod such as RTS Camera Command System) …; cards drawn: 1 Infantry at (20, 833) 131 x 223, 40 men (= formation) | …`;
  without it: `16 cards in 2 sets, set 1 drawn (the game's side columns - keyboard layout)`.
- If RTS Camera's card clicks stop working while the strip is up, that is broken (our strip takes
  no mouse events).

**8d. Resolution, UI scale, gamepad.** Change the game's UI scale once (or play at another
resolution); if you have a gamepad, open the menu with it (the cards move to one row at the top).
- You see: the cells follow the cards at once, still under each card, as wide as the card.
- Log: `[hud] orders strip: the cards changed at … (open #N) - 16 cards in 2 sets, set 2 drawn (the game's top row - gamepad layout); cards drawn: …; cells: …`
  (rate-limited). The bottom card of a column: if its cell would leave the screen it is lifted
  to the edge — the line ends `- 1 cell lifted to the screen's bottom edge` (not expected at the
  defaults: the cell is 23 px deep and fits the 1080p bottom card exactly).
- Too big, too small, too close to the icons? The five Advanced settings (*Orders strip: text
  size / numbers offset / bar offset / bar thickness / side margin*) move it live — tell Claude
  the numbers that look right.

**8e. Switch things mid-battle** (Escape → Mod Options → *Bars - your squads*, then back).
- *Orders menu strip* (`ShowInOrderMenu`) off: no strip at the next open; on again: back.
  Log (the menu open): `[hud] orders strip: layer removed at … - ShowInOrderMenu off`.
- *Show average health* (`ShowFormationHealth`) off: the `HP 81%` goes, the rest stays; on: back.
  Log: `[config] ShowFormationHealth: true → false (source: MCM)`; the next `values at …` line ends `health off (ShowFormationHealth)`.
- *Show the spread* (`ShowFormationSpread`) off: the numbers read `72%` and the band goes.
- *Strip under the cards* (`OrderStripUnderCards`) off: a small dark panel at the top centre
  lists your formations instead (8f). Log: `[hud] orders strip: the compact panel at … (open #N) - OrderStripUnderCards is off; …`.
- *Mod enabled* off: the strip goes at once (`… layer removed at … - ModEnabled off (the master switch)`).

**8f. The fallback panel.** You should NOT see it with the switch above on. If you do: a dark
box at the top centre, one row per formation — `1 Infantry     72% ± 8   HP 81%` over a slim bar.
- Log, with the reason: `[hud] orders strip: FALLBACK to the compact panel at … (open #N) - <why>; the panel lists your formations at the top of the screen (80 px down, 300 px wide) until the menu closes - the next open tries the cards again`.
  The whys: `no MissionOrder layer on the screen and no other layer holds formation cards`,
  `N cards found - not whole sets of 8 …`, `two sets of cards drawn at once …`,
  `no card drawn (of 16 found) 0.60 s after the menu opened`, `… has 12 men but its card is not drawn`,
  `2 Archers's card counts 25 men, the formation has 20 for more than 1.0 s`. Send the log.

**8g. The summary** — two `[summary] hud:` lines for the strip:
```
[summary] hud: orders strip (movie TraxOrderStrip) - on screen 48.1 s of 340.2 s (14%); layer built 12x, removed 12x (orders menu closed 11, mission end 1); hidden: not a fight 20.1 s, orders menu closed 272.0 s; 480 refreshes; errors 0
[summary] hud: orders strip - opened 12x: under the cards in 12, the compact panel in 0; technique: the live vanilla cards (layer MissionOrder: 16 cards); card layouts seen: 16 cards in 2 sets, set 1 drawn; cells placed 36 (lifted to the screen's edge 0), card changes 0, short mismatches 0 (under 1.0 s), card re-scans 0, values pushed 40; fallbacks: none
```
- *opened* = how often you opened the menu; *under the cards in* should equal it; *fallbacks:
  none*; *errors 0*. *Short mismatches* = a card's man-count and ours disagreed for a moment (a
  man fell between two updates) — harmless if small.

**What counts as broken** — tell Claude and send the log:
1. **Misaligned cells**: a cell not under its card (shifted, wrong width, under the wrong card),
   at any resolution / UI scale / with RTS Camera. Compare the `first placement` / `cards
   changed` lines: the card and cell positions there are what the mod believes.
2. **Covering vanilla UI**: our numbers or bar on top of a card, the order icons, the ammo bar,
   the order menu itself — or a cell cut off at the screen edge.
3. **Wrong formation**: the numbers under the Archers' card are the Infantry's (check the
   `values at` line against the troop counts on the cards), or a card with men has no cell.
4. **The panel shows up** with `Strip under the cards` on (8f) — send the `FALLBACK` line.
5. **Stays on screen** after the menu closes, in deployment, with Hide Battle UI on, or when
   switched off; or does not come back.
6. **Numbers that cannot be right**: 100% green for a formation that fought for minutes; `HP`
   not falling when they are cut down; the band not around the fill's end.
7. RTS Camera's card clicks or anything else in the orders menu stop working while the strip is
   up.
8. Any `[error] hud.…` line, or `[hud] orders strip: … DISABLED for the rest of this battle`.

(steps below are added as the features land)

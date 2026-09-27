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

Every log line looks like `2026.09.30 20:15:02.117 [tag] message`. The tags: `[load]`
`[compat]` `[config]` `[mcm]` `[mission]` `[summary]` `[damage]` `[speed]` `[endurance]`
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
  - `[mcm] settings page registered at main menu (attempt 1): MCM 5.…, page "Trax Combat Enhancements", 32 settings in 7 groups…`
    (`at retry (attempt N)` is fine too — MCM was just slow to wake)

**1b. The mod loads — WITHOUT MCM.** Quit, disable *Mod Configuration Menu v5* in the launcher
(Harmony / ButterLib / UIExtenderEx may stay on), start again. Then turn MCM back on.
- You see: the game starts normally; the line now ends *"- settings in config.json."*
- Log: `[mcm] MCM (Mod Configuration Menu) is not loaded - no settings page; the mod runs on config.json alone. That is fine.`
- This is the check that matters most: a mod that hard-needs MCM refuses to start for every
  player without it (the sibling mod's expensive lesson).

**1c. The config file, with explanations.** Open
`Documents\Mount and Blade II Bannerlord\Configs\TraxCombatEnhancements\config.json`.
- You see: a header saying where the file lives and how edits apply, then 7 sections
  (Damage randomness, Endurance, Exhaustion, Regeneration, Bars - you and your target,
  Bars - your squads, Advanced) and above EVERY key a `//` explanation in plain words ending
  `(default …, range … to …)`.
- Log, first run only: `[config] first run: created config.json with every default and a plain-words explanation beside each value`
- Log, every start: `[config] settings in effect (32, version 0):` followed by 32 lines like
  `[config]   DamageRandomPercent = 50` — a value you changed shows `(default 50)` after it.

**1d. The MCM page.** Main menu → Options → Mod Options → *Trax Combat Enhancements*.
- You see: the same 7 groups, 32 settings: checkboxes for the on/off ones, sliders for the
  numbers (the slider ends are the ranges from the file). Hovering a setting shows its
  explanation and default. The preset list has *Default*; the page's Reset puts every value
  back to the defaults in DESIGN's table (not to what was loaded).

**1e. A change in MCM applies at once — even mid-battle.** In a battle, Escape → Options →
Mod Options → move *Spread (± %)* from 50 to 40, press Done.
- Log, the moment the slider moves (dragging logs each step; Cancel logs the way back):
  `[config] DamageRandomPercent: 50 → 40 (source: MCM)`
- Log, on Done: `[mcm] Done pressed - writing config.json` then
  `[config] wrote config.json (MCM Done): from MCM DamageRandomPercent`
- The file now says `"DamageRandomPercent": 40`. No restart asked for, ever.

**1f. A hand edit is picked up at the next battle.** Alt-Tab, change `"MaxEndurance": 100` to
`150` in config.json, save, start (or enter) the next battle.
- Log: `[config] MaxEndurance: 100 → 150 (source: file)` and
  `[config] config.json re-read at mission start: 1 change(s), settings version N`
- Every mission start writes `[config] config.json re-read at mission start: no changes`
  when nothing changed.
- **The rewrite rule** (optional, 1 minute): hand-edit one value, then — BEFORE the next
  battle — change a DIFFERENT value in MCM and press Done. The hand edit must still be in the
  file. Log: `… kept hand edit(s) from the file that apply at the next battle start: MaxEndurance = 150 (now 100)`.
- Mistakes are safe: a typo'd key → `[config] file problem: "Maxendurence" is not a setting of this version - ignored (typo?)`
  (kept in the file under "Not recognised"); a bad number → `[config] could not read config.json at mission start (line …)`
  and the values stay as they were.

**1g. Mission start and end, and the summary.** Fight any battle to the end (or retreat).
- Log, at the start:
  - `[config] config.json re-read at mission start: …`
  - `[mission] attached: EnduranceLogic (views arrive with steps 6-9)`
  - `[mission] start: scene <scene id>, field battle, mode …, combat type Combat, game Campaign, agents so far …`
  - `[mission] first tick: N agents active, …` and `[mission] deployment finished: N agents active, mode Battle`
- Log, at the end — the `[summary]` block:
  - `[summary] ==== scene …, field battle - ended (mission end) after N s, result: player victory ====`
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
- `[speed] agent stat model decorator registered over … (pass-through until step 5); tournament AI-level fix active over N base model(s)`
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
[summary] damage rolls: 812 hits (melee 650, ranged 120, mounts 40, shields 2); factor min 0.50 / avg 1.002 / max 1.50; damage 21934 → 22011 (+0.4%)
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

(steps below are added as the features land)

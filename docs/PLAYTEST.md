# Playtest — one session, start to finish

Anton plays this ONCE, after the build is finished, and hands Claude the log. Seven parts in
order, about **2 hours** (A 10 min, B 15, C 25, D 10, E 15, F 35, G 10; the optional bits add ~30). Each part says what to do, what you
should see, what counts as broken, and the log lines that prove it. The full line reference —
every summary block and what proves what — is the **appendix** at the end.

**While you play**: when something looks or feels wrong, note the clock time (the log uses your
PC's clock) and a few words, then carry on. The log was built so that one run is enough. Stop
only if the game becomes unplayable.

**VerboseLogging: ON for the whole session** (you switch it on in A2). A trim never cuts what
matters: past the size limit only the oldest *verbose* lines go — every summary, every "first
time this battle" line, every setting and every error stays (DESIGN §4 "The log"). In a big
battle the log trims itself every minute or two; if you feel a short hitch at those moments,
note the time (Advanced → *Log size limit (MB)* 32 makes it rarer).

**The log**: `Documents\Mount and Blade II Bannerlord\Configs\TraxCombatEnhancements\trax_combat.log`,
beside `config.json`. Every line is `2026.09.30 20:15:02.117 [tag] message`; a verbose line has a
`~` before its tag: `2026.09.30 20:15:02.201 ~[damage] melee on a person: …`. Tags: `[load]`
`[compat]` `[config]` `[mcm]` `[mission]` `[summary]` `[damage]` `[athletics]` `[speed]` `[rate]`
`[stepback]` `[hud]` `[error]` `[log]` — search one to follow an area. Every battle ends with a
`[summary]` block; Claude reads those first.

**Any time, anywhere — broken**: an `[error]` line (the game also shows a red line *"Trax Combat
Enhancements: an error was caught and logged…"*; the mod keeps going with vanilla behaviour for
that spot); a crash; a battle that never ends.

---

## Before you start (5 min)

- The build is deployed (`powershell -ExecutionPolicy Bypass -File tools\deploy.ps1`); the launcher
  lists **Trax Combat Enhancements (dev)**.
- **RBM (Realistic Battle Mod) off** — not compatible (it has its own posture and stamina). If it is
  on, the main menu shows a yellow line *"Trax Combat Enhancements is not compatible with Realistic
  Battle Mod - disable one of them."* and the log says `[compat] Realistic Battle Mod is ENABLED (…) - NOT compatible…`.
- In `Configs\TraxCombatEnhancements\`, rename the old `trax_combat.log` and `config.json` (e.g. add
  `.old`): the session starts on a clean log and a fresh config file.
- Game options → Gameplay → *Report Damage* on (the default): the "Delivered N damage" lines.

---

## A. Install and load — with and without MCM (10 min)

**A1. Start WITHOUT MCM, and without RTS Camera.** In the launcher enable only the dev mod (Harmony,
ButterLib, UIExtenderEx may stay on). Start the game. This is the check that matters most: a mod that
needs MCM refuses to start for every player without it.
- You see: a normal start; at the main menu *"Trax Combat Enhancements 0.1.0… loaded - settings in
  config.json."* No error dialog.
- Log:
  - `[load] ==================== Trax Combat Enhancements 0.1.0+… ====================` (after the `+`:
    the git commit it was built from), `[load] dll: …\Modules\TraxCombatEnhancements.Dev\bin\Win64_Shipping_Client\TraxCombatEnhancements.dll (built …)`
    (the dev copy runs), `[load] module: TraxCombatEnhancements.Dev (the dev install - tools\deploy.ps1)`,
    `[load] game: v1.4.8.…`, `[load] modules (N): …`
  - `[compat] Realistic Battle Mod (RBM) not enabled - good`
  - `[config] defaults: defaults.json (embedded in TraxCombat.Core.dll), 59 keys for 59 settings - every default read from it`
  - `[config] first run: created config.json with every default and a plain-words explanation beside each value`
  - `[config] settings in effect (59, version 0):` and 59 lines like `[config]   DamageRandomPercent = 50`
  - `[mcm] MCM (Mod Configuration Menu) is not loaded - no settings page; the mod runs on config.json alone. That is fine.`
    — or, if another enabled mod carries MCM's DLL in its own folder (step 12, your 21:08 session):
    `[mcm] MCM's module is not enabled - no settings page; config.json only. (An MCM 5.… DLL is loaded - another mod carries it - …)`
    — one line, and no `[mcm]` line after it.
- Broken: the game will not start or names the mod in an error; no `[load]` lines; any
  `defaults.json PROBLEM:` line; `[mcm] … not ready yet` with MCM's module off.

**A2. The config file — and VerboseLogging on, by hand.** Alt-Tab, open `config.json`.
- You see: a header (where the file lives, that edits apply at the next battle start, three ways back
  to the defaults), then 9 sections — *Master switch, Damage randomness, Athletics, Tired fighters,
  Tired fighters step back, Refill, Your Athletics bar, Orders menu strip, Advanced* — and above
  every key a `//` explanation ending `(default …, range … to …)`.
- Change `"VerboseLogging": false` to `true` and save. Read a few explanations as a new player
  would; note any that confuse you.

**A3. A 3-minute custom battle — the game's own order cards.** Custom battle, you as general, your
side with infantry, archers and cavalry. Fight a minute, open the orders menu once (keyboard), then
end it (win or retreat).
- You see: your Athletics bar under the health bar (E1); under each of the game's formation cards
  — two columns at the screen's edges — a small strip `100% ± 0 … HP 100%` over a green bar (E2).
- Log:
  - `[config] VerboseLogging: false → true (source: file)` and `[config] config.json re-read at game start: 1 change(s), settings version 1`
    — the hand edit reached the game without a restart;
  - `[hud] orders strip: first placement under the cards at … (open #1) - technique: the game's own cards read live, layer MissionOrder: 16 cards = 16 cards in 2 sets, set 1 drawn (the game's side columns - keyboard layout); …`
  - at the end a `[summary]` block (appendix L2).

**A4. Now WITH MCM (and RTS Camera, as you usually play).** Quit. Enable *Mod Configuration Menu v5*
(with Harmony, ButterLib, UIExtenderEx) and RTS Camera + Command System. Start.
- You see: *"… loaded - settings in Mod Options."*
- Log: `[mcm] settings page registered at main menu (attempt 1): MCM 5.…, page "Trax Combat Enhancements", 59 settings in 9 groups, format "none" (config.json is the only store), Default preset = the mod's defaults (defaults.json); group "Defaults": buttons "Revert all to defaults" and "Save current values as a defaults file".`
  (`at retry (attempt 2)` is fine — MCM woke a moment after us; the first try is now always at the
  main menu, where MCM builds its services — step 12); the settings dump shows
  `VerboseLogging = true (default false)`.
- Broken: `[mcm] MCM … never became ready - gave up after 31 attempts …` with MCM enabled (the page
  is missing — send the `[mcm]` lines); a `not ready yet` line with no `registered` or `gave up` after it.

**A5. The MCM page.** Options → Mod Options → *Trax Combat Enhancements*.
- You see: the same 9 groups — *Master switch* (Mod enabled) on top, *Advanced* at the bottom, and
  just above Advanced a *Defaults* group with two buttons. Checkboxes for switches, sliders for
  numbers (their ends are the file's ranges), each label with its unit (points, %, x, m, s, px, MB).
  Hovering shows the explanation, *"Applies at once, even mid-battle."* and the default. *Verbose
  log* is ticked — read from the file.
- Broken: a group missing or out of order; a label or hint that reads wrong; a slider that cannot
  reach its default.

**A6 (optional, +5 min). The release package, beside the dev copy** (step 11). Run
`powershell -ExecutionPolicy Bypass -File tools\package.ps1` (add `-Force` if it says the zip exists),
copy the folder `dist\TraxCombatEnhancements` into the game's `Modules\`, enable BOTH *Trax Combat
Enhancements* and *Trax Combat Enhancements (dev)* in the launcher, start, look at the main menu, quit.
Then DELETE `Modules\TraxCombatEnhancements` again (the Workshop copy will carry the same id).
- You see: the exact folder the Workshop gets starts cleanly; ONE yellow line *"Trax Combat
  Enhancements: 2 copies are enabled (…) - only … runs. Disable one in the launcher."*
- Log: `[load] module: …` names the copy that loaded first; `[compat] 2 copies of this mod are enabled (…) - this one (…) loaded first and runs; …`;
  at the main menu `[compat] 1 other copy of this mod found this one (…) running and stood down: … (reported at main menu)`.
  Only ONE `[load] ====…====` block — the other copy writes nothing.
- Broken: two `[load]` blocks, or `[damage] damage model decorator registered` twice at a game start
  (every hit would roll twice); `[compat] WARNING: 2 copies … but none stood down`.

---

## B. The master switch — the same battle ON and OFF (15 min)

*Mod enabled* (MCM → *Master switch*; the first key of config.json) turns EVERY feature off at once,
live — no damage rolls, no Athletics, no slow attacks, no step backs, no bars — while the log keeps
recording, so two runs of the same battle compare line by line.

**B1. Run ON.** A custom battle you can repeat exactly: the same map, the same two armies — e.g.
100 Imperial Legionaries (you as general) against 100 Imperial Recruits, infantry only. Charge, then
watch (free camera or RTS Camera) to the end. Note the setup.
- You see: hits vary; the recruits tire first; men step back from the front; a readable fight.
- Log: `[mission] start: scene <scene id>, field battle, mode …, combat type Combat, …, agents so far …, mod ON`;
  the summary header `[summary] ==== scene …, field battle - ended (mission end) after N s, result: …, mod ON ====`.

**B2. Run OFF.** The same setup; BEFORE the battle: MCM → *Mod enabled* off.
- You see: plain vanilla — the same blow deals the same number, nobody slows, nobody steps back, no
  bar, no strip.
- Log: `[mission] start: … mod OFF (ModEnabled) - this battle runs as vanilla; the log still records it for comparison`,
  `[damage] mission start: mod OFF (ModEnabled) - no hit is rolled, the game's own numbers are recorded for comparison; …`;
  summary: `… mod OFF ====`, `damage while the mod was OFF - the game's own numbers, not rolled (factor 1.00): N hits (…); damage X → X (+0.0%), avg … per hit`,
  `Athletics settings at the end: OFF - the whole mod is switched off (ModEnabled) - everyone full, no penalty`.
- Compare the two summaries (Claude will too): duration; `people removed: N killed, N knocked out,
  N fled`; hit counts; the two "avg … per hit" numbers (close — the roll is fair on average).

**B3. Flip it mid-battle** (3 min). *Mod enabled* on again; start the same battle, fight until men
are tired, then Escape → Mod Options → *Mod enabled* off → Done → fight ~20 s → on again → retreat.
- You see: the moment you are back, every slow fighter swings at full speed, damage stops varying,
  your bar and the strip are gone, anyone stepping back walks back to his line; switched on,
  everyone starts fresh (full bars).
- Log: `[config] ModEnabled: true → false (source: MCM)`; the next frame of the battle
  `[mission] mod switched OFF (ModEnabled) at 42.3 s - vanilla from now on: …`, `[athletics] the whole mod (ModEnabled) switched OFF mid-mission: N fighters back to full, M speed penalties lifted …`,
  `[stepback] the whole mod (ModEnabled) switched OFF mid-mission at …: N fighters stepping back released to their formations at once`,
  `[rate] ModEnabled switched OFF mid-mission at … s: N held fighters may attack again at once`,
  `[hud] player bar: layer removed at … - ModEnabled off (the master switch)`; on again
  `[mission] mod switched ON (ModEnabled) at …` and `[athletics] the whole mod (ModEnabled) switched ON mid-mission: everyone starts full`;
  the summary header `…, mod was on for 70% of the battle (started ON; OFF at 42.3 s, ON at 64.0 s) ====`.
- Broken: anything still slow, stepping back, rolling or on screen while it is off; the header not
  saying OFF / the share. **Leave it ON from here.**

---

## C. Athletics — how it feels (25 min)

Every fighter has an Athletics bar **as big as his Athletics skill** (never under 50). Every attack
costs points — 10 for a soldier, 7.5 for a hero, 5.6 for the hero who leads his party (you). The top
quarter of the bar (from the **peak line**, the white mark, up) is full strength; below it his lucky
hits, attack RATE (wind-up, swing, draw, reload and, for the AI, the pause between attacks) and run
speed fall in straight lines — at empty one attack where he made five, a run at 0.3. Wounds cap the
bar. About 3 s after the last attack it refills: 60 s from empty to full standing or walking, half
that fast running flat out. Blocking is never slowed.

| Fighter | Athletics skill | Bar | A blow costs | Blows at full strength | Blows to empty |
|---|---|---|---|---|---|
| Recruit / tier-2 soldier | 20 / 40 | 50 (the floor) | 10 | 2 | 5 |
| Elite cataphract | 60 | 60 | 10 | 2 | 6 |
| Legionary | 130 | 130 | 10 | 4 | 13 |
| Fian champion | 170 | 170 | 10 | 5 | 17 |
| You as general in a custom battle (≈ 90) | 90 | 90 | 5.6 | 5 | 16 |
| A 300-skill party leader | 300 | 300 | 5.6 | 14 | 54 |

Setup: custom battle, you as general, your side Imperial Legionaries plus a few archers, the enemy
Imperial Recruits plus a few archers. Keep this battle going through C1-C6.

**C1. You — full strength, weaker every swing, empty, refill.** Before the lines meet, swing at the
air (misses cost too), counting.
- You see: the number drops ~5.6 a swing; the first ~5 swings at normal speed, the bar green down to
  the white mark; then every swing a little slower and the bar blue → yellow → orange → red; at
  ~16 the word reads *Exhausted* in red and a swing takes about five times as long. Run: about a third
  of your pace. Block: normal. Stop: about 3 s later the bar climbs, ~45 s to green, 60 s to full.
- Then empty yourself again and RUN flat out while it refills: about twice as long.
- Log (always):
  - `[athletics] YOU: Athletics skill 90 → pool 90 (the skill x1.00, at least 50); full strength down to 68 (75%); a blow costs you 5.6 - about 5 blows at full strength, 16 to empty`
    — **the skill must match your character screen**
  - `[athletics] YOU dropped below full strength at … s: 61.9 of 90 (the line is 68) after 5 blows this mission - f 0.92: attacks x0.93, run x0.94, damage upside 92% of the full`
  - `[athletics] YOU are exhausted at … s: 0 of 90 after 16 blows this mission - attacks at 20% speed, run x0.30, no damage upside until you rest (…)`
  - `[athletics] YOU are off empty at …`, `[athletics] YOU are back at full strength at …`,
    `[athletics] YOU are back to full at … s: 0 → 90 of 90 in 60.0 s of refill (at a walk or slower 60.0 s, faster 0.0 s; avg rate x1.00; …)`
    — after the run `… avg rate x0.5…`
  - the colours: `[hud] player bar: BLUE for the first time this battle at … - f 0.97, …`, then
    `YELLOW`, `ORANGE`, `RED`, `[hud] player bar: EXHAUSTED shown at …`
- Broken: your pool is not your Athletics skill; no slowdown at empty; a swing that costs nothing;
  the number counting UP while you swing; a colour that disagrees with the fill (green below the mark).

**C2. Recruits run dry, legionaries keep going.** Let the lines meet; watch the front from close by.
- You see: recruits slow after 2 swings and crawl after ~5 — between swings they stand, guard up,
  still blocking; legionaries keep their pace for ~4 swings and last ~13; exhausted men lag when a
  formation moves and fresh men overtake them.
- Log:
  - verbose samples: `~[athletics] pool at spawn: Imperial Recruit - Athletics skill 20 → pool 50 (the floor); 2 blows at full strength, 5 to empty`,
    `~[athletics] blow melee (on foot): … - cost 10.0, 40.0 → 30.0 of 50 (f 1.00 → 0.80) - below full strength`
  - once: `[speed] first exhaustion this mission: <name> at … - properties before: … → after UpdateAgentProperties: … - the penalties are in the agent's properties`
  - once: `[rate] first slowed fighter this mission: <name> at … - attacks x0.97 …; T1 animations: … (each x0.97 as asked); T2 AI decisions (AttackRateAiDecisions on): … (chances x0.97, the aim ÷ 0.97 as asked); …`
    — **`NOT … as asked - tell Claude`**
  - once: `[rate] first pace hold this mission: <name> at … - … held 0.40 s (until … s); scripted flags 0 → 2 (NoAttack set: the engine took it)`
    — **`NoAttack NOT set` → tell Claude**
- The summary (appendix L4, L5) settles whether the engine honours it:
  `attack rate, melee, AI - verdict: ON TARGET in 3 of 3 tired bands`, `run speed check, on foot … - the engine's top speed follows the curve`,
  `Athletics tick cost: avg … ms` (well under 1 ms).
- Feel: tired men fight in slow motion — fewer blows, longer gaps, guard up in between — while
  fresh men keep their pace. Say if it is too much or too little.

**C3. Wounds.** Take a few hits (stand in front of their archers).
- You see: the right end of your bar goes dark red-brown — the part your wounds hold; the refill
  stops at its edge; below about 75% health the bar never turns green again.
- Log: `[athletics] YOU are wounded at … s (62% health): Athletics capped at 55.8 of 90 (cut 11.2) - f now 0.83; full strength needs 68, out of reach until healed`
  and `[hud] player bar: wounded at … - the last 38% of the bar shown dark: usable 56 of 90 (health caps the bar); f can reach at most 0.83 now`.

**C4. Shoot.** Take a bow (or javelins) and shoot until empty; watch the two groups of archers.
- You see: each draw a little slower below the peak line, very slow at empty; tired archers' volleys
  thin out.
- Summary: `attack rate, ranged, AI - verdict: …`; `Athletics detection: … shots seen N … | ranged releases seen by the poll M` (N ≈ M).

**C5. Ride.** Mount, swing from the saddle; couch a lance if you have one; then ride 30 s without
attacking.
- You see: the same curve on horseback; riding itself never drains you; horses keep their speed.
- Summary: `Athletics detection: … melee hits by fighters H (…): during a counted release A, outside one B` — **B small next to A**.

**C6. Change it mid-battle (hot swap).** Escape → Mod Options → Trax Combat Enhancements; every change
applies on the first frame back.
- *Tired fighters* → *Attack speed when empty (%)* 20 → 50: empty men attack at half speed instead of
  a fifth. Log `[config] ExhaustedAttackSpeedPercent: 20 → 50 (source: MCM)`, then
  `[speed] speed settings now: when empty attacks at 50%, run x0.30, horses x1.00; full strength at 75% of the pool - N fighters get new speeds …`. Back to 20.
- *Damage randomness* → *Spread (± %)* 50 → 0: the same blow deals the same number. Log per hit
  `~[damage] not rolled - spread 0 (DamageRandomPercent): melee on a person: …`. Back to 50: the very
  next hit rolls again.
- *Athletics* → *Athletics* off: every slow fighter is normal at once, your bar goes. Log
  `[athletics] AthleticsEnabled switched OFF mid-mission: N fighters back to full, M speed penalties lifted …`;
  on again `… switched ON mid-mission: everyone starts full …`. Then finish the battle.

**C7 (optional, +10 min). The attack-rate A/B.** Three short runs of C's setup, flipping BETWEEN
battles: (1) the defaults; (2) *Tired fighters* → *Tired AI keep a slower pace* off (the AI's
decisions alone); (3) it on and *Tired AI attack less often* off (the hold alone). Compare each
summary's `attack rate, melee, AI - verdict` line and tell Claude which read closest to ON TARGET —
and which FELT right.

---

## D. Tired men step back (10 min)

After a melee swing, an AI fighter on foot below full strength may step back ~2 m straight away from
the man he fights, facing him, guard up, no swings, for up to 1.5 s — then his formation takes him
back. Chance = 100% × (1 − f): never at full strength, every swing at empty. Never you, never riders,
never after a shot; field battles only; not from a shield wall, square or circle.

**D1. See it.** Custom battle, infantry against infantry (60 v 60, no archers, a flat map). Let the
lines meet; after a minute watch the front from close by (or from the side).
- You see: a man who has swung a few times backs off a step or two, still facing the enemy, shield
  up, and walks back into the line a moment later; the men beside him keep fighting; it happens more
  as the fight wears on; the line keeps its shape.
- Log:
  - `[stepback] mission start: ON - after a melee swing an AI fighter on foot steps back with chance 100% x (1 - f) …`
  - `[stepback] first step back this mission: <name> at 63.2 s (f 0.40, Athletics 20.0 of 50, chance 60%, …) - from (…) to (…) (2.00 m straight away from <enemy>, …); facing before: 12° off his enemy; formation 1 Infantry (…); scripted flags none → GoToPosition|NoAttack|… (GoToPosition set: the engine took it)`
  - `[stepback] first step back ended (time up) after 1.5 s: … moved 1.30 m …; mid-step facing his enemy (20° off), moving away (…); hits taken 1 (blocked 1), swings 0; released: scripted movement off, …`
  - verbose: `~[stepback] step back: …`, `~[stepback] step back ended (…): …`, `~[stepback] step back not started: … - <reason>`

**D2. Force it.** *Athletics* → *Smallest bar (points)* 50 → 10 (recruits empty after two blows).
- You see: most tired men step back after every swing — the front "breathes" back and forth but
  keeps its shape. Set it back to 50.

**D3. Off mid-battle.** *Tired fighters step back* → *Tired fighters step back* off → Done.
- You see: anyone stepping back walks straight back into his formation; nobody steps back any more;
  on again, they start from their next swing.
- Log: `[stepback] StepBackEnabled switched OFF mid-mission at …: N fighters stepping back released to their formations at once`.

**Broken — tell Claude** (the summary's step-back lines, appendix L6, carry the proof):
1. **Men turn their backs** to walk away — THE risk. Then set *Chance when empty (%)* to 0.
2. **Men stuck** behind the line or frozen — every `(must be 0)` in the `release check` line must be 0.
3. **Formations fall apart** — try *Most at once (whole battle)* 10–20 and say which felt right.
4. **They drop their guard** while stepping back (the `guard` line) — try *No swings while stepping back* off.
5. `swings started while stepping back` above 0 with *No swings…* on.

---

## E. Your Athletics bar and the orders-menu strip (15 min)

**Your bar** — bottom right, one row under the vanilla health bar (and the horse bar when you ride),
its right end lined up with the health bar's fill: the word *Athletics*, the number `132 / 180`
(left / your whole bar; it reads 0 only when truly empty), and a slim bar: the fill coloured by f
(green at or above the peak line, blue just below, yellow ≤ 75% of the line, orange ≤ 50%, red ≤ 25%),
a white mark at the peak line, the part your wounds hold dark red-brown, the rest dark grey. Empty:
*Exhausted*, word, number and frame red.
```
                                     [♥ ████████████ health ████████████ ]
             Athletics  180 / 180   [██████████████████████████|███████]     all green, mark at 3/4
             Athletics   71 / 180   [██████████░░░░░░░░░░░░░░░░|░▓▓▓▓▓▓]     yellow fill, grey, dark wounded end
             Exhausted    0 / 180   [░░░░░░░░░░░░░░░░░░░░░░░░░░|░▓▓▓▓▓▓]     all red text and frame
```

**The strip** — while the orders menu is open, under each of your formation cards: left `72% ± 8`
(the men's average Athletics as a share of their own bars, ± their spread), right `HP 81%` (their
average health), the middle free for the game's order icons; under those a slim bar as wide as the
card — the average fill in the colour of the men's strength, a lighter ± band, a tick at the peak
line. You are not in the numbers (you have your own bar).
```
   ┌──────────────┐
   │ morale  83   │     the game's card: morale, troops, captain,
   │ ⚔ 40         │     and at its bottom the ammo bar
   │   ───────    │
   └──────────────┘
   72% ± 8 [⚑◎] HP 81%       ← ours: numbers left and right of the game's order icons
   [██████████▒▒▒│░░░░]      ← ours: fill (colour by f), lighter ± band, tick at the peak line
```

Any custom battle with several formations (keep RTS Camera on, as since A4).

**E1. The bar — when it shows and where.** In a battle it appears when the fight starts with you on
the field; never during deployment, conversations, cutscenes, with *Hide Battle UI* (Options →
Gameplay) or photo mode on, or once you are knocked out. Outside a battle — the training field, a
town — it shows while you hold a weapon or a shield, or while your Athletics refills (F6, step 12).
Try: *Hide Battle UI* on and off; get knocked out (or watch the end of a battle); change the game's
UI scale once; mount a horse.
- You see: the row under the health bar (and the horse bar), not overlapping them, not cut off; it
  scales with the UI scale like the health bar; it goes and comes back with each switch.
- Log: `[hud] attached: player bar (PlayerAthleticsView, movie TraxPlayerAthleticsBar, prefab installed) - …`;
  `[hud] player bar: layer created at 31.2 s (mode Battle, was hidden: not a fight (mission mode)) - movie TraxPlayerAthleticsBar loaded OK (13 widgets)`;
  `[hud] player bar: first values pushed at … - 90 / 90, fill 1.00, …, colour green (peak zone - full strength), …`;
  `[hud] player bar: layer removed at … - the game's Hide battle UI is on` / `- no player agent on the field` / `- ShowPlayerBar off`.
- If it sits wrong: move it live with *Advanced* → *Your bar: length / thickness / from the right
  edge / from the bottom edge (px)* until it looks right; tell Claude the four numbers and your
  resolution and UI scale.
- War Sails: while you steer a ship the vanilla hero bar shrinks and drops — say whether ours collides.

**E2. The strip under the cards — with RTS Camera.** Open the orders menu.
- You see: a cell under every card with men, lined up with the card's left and right edges; nothing
  covering the cards, the order icons or the ammo bars; with RTS Camera the columns run bottom to top
  (Infantry is the bottom-left card) and its always-shown order icon sits between our numbers.
- Log: `[hud] orders strip: first placement under the cards at … (open #1) - technique: the game's own cards read live, layer MissionOrder: 8 cards = 8 cards in 1 set, set 1 drawn (one layout - an order-menu mod such as RTS Camera Command System); screen 1920 x 1080 px, UI scale 1.00; cards drawn: 1 Infantry at (20, 833) 131 x 223, 40 men (= formation) | …; cells (…): 1 Infantry at (20, 1057) w 131 | …`
  — every drawn card must say `(= formation)`; `[hud] orders strip: values at … (open #1, under the cards): 1 Infantry 72% ± 8 HP 81% (40 men, f 0.93) | …`
  (A3 gave the vanilla line: `16 cards in 2 sets, set 1 drawn (the game's side columns - keyboard layout)`.)

**E3. Fight, then look again; switch things.** Let the infantry fight a while, open the menu.
- You see: the infantry's number and fill lower, the colour moving down the scale, the band wider when
  some men are fresh and some spent; `HP` dropping; a formation that has not fought stays green at 100%.
- *Orders menu strip* → *Show average health* off: the `HP` goes; *Show the spread* off: `72%` and no
  band; *Strip under the cards* off: a small dark panel at the top centre lists your formations
  instead (`[hud] orders strip: the compact panel at … (open #N) - OrderStripUnderCards is off; …`).
  Turn each back on. Change the UI scale once: the cells follow the cards at once.
- Too big, too small, too close to the icons? *Advanced* → *Orders strip: text size / numbers offset /
  bar offset / bar thickness / side margin (px)* move it live — tell Claude the numbers that look right.

**Broken — tell Claude**: no bar at all (read the `[hud]` lines in order: no `attached:` → look for
`[error] hud.attach`; `prefab NOT FOUND` → the GUI folder did not install; `movie … FAILED to load`);
the bar or a cell in the wrong place, overlapping vanilla UI or cut off; a colour that disagrees with
the fill or its `for the first time` line's f; a wrong number (not your pool, counting up, 0 with fill
left); the wounded part missing; staying on screen when it should hide, or not coming back; a cell
under the wrong card or a card with men and no cell; the **compact panel with *Strip under the cards*
on** (send the `FALLBACK` line — appendix L7); the mouse snagging on our layer, or RTS Camera's card
clicks not working while the strip is up; `[hud] … DISABLED for the rest of this battle`.

---

## F. Campaign — a field battle, a siege, a tournament, the training field (35 min)

Load (or start) a campaign with a companion or two in your party.

**F1. A field battle** — ideally inside an army, with lords on both sides.
- You see: the same Athletics as in C; heroes last longer than soldiers.
- Log: `[athletics] party leader: <lord> - Athletics skill … → pool … (full strength down to …), pays x0.56 per blow (5.6 now)` for each leader;
  verbose `~[athletics] hero: <companion> - Athletics skill … → pool …, pays x0.75 per blow (7.5 now)`;
  blows: soldiers `cost 10.0`, your companion `cost 7.5 (x0.75: hero)`, you and a lord leading his own
  party `cost 5.6 (x0.56: hero party leader)`; summary `[summary] Athletics heroes: N flagged, M party leaders (<names>); lowest a hero reached: …`.
- In an army only each party's OWN leader pays the leader price (not the marshal for everyone).

**F2. A siege** (attack or defend).
- You see: nobody steps back on ladders, in siege towers, at siege engines or over a wall's edge.
  Jump off a wall; hit a gate or a siege engine.
- Log: `~[damage] not rolled - fall damage: fall on a person: … → <you> (you), no weapon, 12`,
  `~[damage] not rolled - objects (doors, siege engines, ships): melee on an object: …`; the summary's
  step-back `not started` reasons (`busy (the game's own check: …)`, `spot not level (wall edge, stairs)`, `no straight way back (wall, fence, gap)`).
  A garrison lord pays the hero price only.

**F3. A tournament** (a few rounds).
- You see: your Athletics bar in the tournament too (Athletics is on in tournaments and the arena —
  an open question: say whether it feels right); nobody steps back; the opponents get tougher each
  round as in vanilla.
- Log: `[stepback] this mission is a tournament or arena fight (…) - no step backs here (vanilla AI)`;
  each round `[speed] tournament AI level multiplier 1 → 1.33 passed on to N base stat model(s)` (our
  hook must not swallow the game's round difficulty).

**F4 (optional, +5 min). Arena practice** in a town — the cleanest attack-rate reading (no step backs
there). Fight a recruit-level opponent with a shield and block his swings: his first 2 swings at the
fresh rhythm, then each one later; empty, about one swing where he made five, guard up in between.
Your bar is up the whole time (a weapon in hand).

**F5 (optional, +10 min). A hideout.** The stealth phase costs Athletics too (the bar shows); the boss
fights you in battle mode and may step back when tired — say if that feels wrong.

**F6 (5 min). Your bar outside a battle — the training field, then a town** (step 12: your first
playtest found no bar in the training field — it runs in the game's walk-about mode, not a battle
mode). In the training field: walk about with empty hands, then take a weapon from a rack (or draw
yours), swing at a dummy or spar with the trainer, put the weapon away; talk to the trainer. Then
walk into a town with your weapon sheathed.
- You see: no bar with empty hands and a full bar; the bar the moment a weapon (or a shield) is in
  your hand; switching weapons does not make it blink; put away after a few swings, it stays while
  the number climbs back and goes about a second after it is full; gone while you talk to the trainer
  (back after the talk if you still hold the weapon); in town with the weapon sheathed, no bar.
  *Your Athletics bar* → *Your bar outside battles too* off: no bar outside a battle at all (fights
  unchanged).
- Log: `[hud] player bar: not shown at … - outside a battle: no weapon drawn and your Athletics full`;
  `[hud] player bar: layer created at … (mode StartUp, outside a battle: a weapon drawn, was hidden: …) - movie TraxPlayerAthleticsBar loaded OK (13 widgets)`;
  `[hud] player bar: layer removed at … - outside a battle: no weapon drawn and your Athletics full`;
  `… - not a fight (mission mode) - mode Conversation`; with the switch off
  `… - outside a battle, and ShowPlayerBarOutsideBattles is off`; the summary's `hud: player bar` line
  ends `outside a battle: shown Nx (weapon drawn N, refilling N), on screen N s; errors 0`.
- Broken: no bar with a weapon in hand in the training field (send the `[hud]` lines); a bar during a
  conversation or with empty hands and a full bar for more than a second; the bar blinking while you
  switch weapons; `outside a battle, and this mission does not track your Athletics` in the training
  field.

---

## G. Tuning — and save it as a defaults file (10 min)

**G1. Tune.** Anything that felt wrong — change it in MCM, mid-battle if you like, and watch it
apply. Log: `[config] <Key>: <old> → <new> (source: MCM)` for each change; `[mcm] Done pressed - writing config.json`
and `[config] wrote config.json (MCM Done): from MCM …` on Done.

**G2. Save current values as a defaults file.** MCM → *Defaults* → **Save current values as a
defaults file**.
- You see: a green line with the path — `…\Configs\TraxCombatEnhancements\defaults.json`. Nothing in
  the game changes. The file opens in any editor: the same explanations as the mod's defaults.json,
  your values.
- Log: `[mcm] "Save current values as a defaults file" pressed` and
  `[config] saved the current values as a defaults file: <path> - N differ from the built-in defaults: DamageRandomPercent 35 (built-in 50), …`.

**G3. Revert all to defaults** — AFTER G2, so your tuning is saved first. MCM → *Defaults* →
**Revert all to defaults**.
- You see: every slider jumps back at once; a green line *"Trax Combat Enhancements: every setting is
  back to its default (N changed) - applied now; config.json saved."* (MCM's Cancel does not undo it.)
- Log: `[mcm] "Revert all to defaults" pressed`, one `[config] <Key>: … → … (source: defaults)` per
  setting that moved, `[config] wrote config.json (reverted to defaults): every value as it is in effect now`,
  `[config] reverted all 59 settings to their defaults (…): N changed, applied live`.

**G4 (optional, 1 min). The rewrite rule.** Hand-edit one value in config.json; then — before any
battle — change a DIFFERENT value in MCM and press Done. The hand edit must still be in the file.
Log: `… kept hand edit(s) from the file that apply at the next battle start: <Key> = … (now …)`.

---

## What to send Claude

From `Documents\Mount and Blade II Bannerlord\Configs\TraxCombatEnhancements\` (zip the folder):
1. **`trax_combat.log`** — the whole session.
2. **`config.json`** — the values you ended with.
3. **`defaults.json`** — the file G2 wrote: your tuning.
4. **Your notes, with times**: what looked or felt wrong and when; the bar and strip placement numbers
   you settled on (E1, E3) with your resolution and UI scale; which mods were on.
5. **Your verdicts** on the open questions: the hero price (0.75 now, or 0.5?); kicks and shield bashes
   free (now) or costing Athletics?; Athletics in tournaments and the arena (on now)?; which attack-rate
   switch felt right (C7); the balance — recruits empty after 5 swings, one attack in five at empty, a
   run at 0.3, 45 s to full strength at rest: too harsh, too soft, right?
6. Optional: screenshots of the bar and the strip.

---

## Appendix — the log, line by line

### L1. Load, settings, MCM

- Load: `[load]` lines at every game start (A1), incl. `[load] module: <Id>` - which copy runs (dev or
  release); `[compat]` RBM or not; `[compat]` two copies enabled and the other stood down (A6 - only
  when both are on); `[mcm]` registered or not (A1, A4) — step 12: at most ONE `not ready yet` line,
  followed by `registered at retry (attempt N)` or one `never became ready - gave up after 31 attempts …`;
  with MCM's module off but its DLL carried by another mod, one `MCM's module is not enabled - no settings page; config.json only.`
  and nothing more; `[config] defaults: … 59 keys for 59 settings` (any `defaults.json PROBLEM:` under it →
  that setting runs on a fallback — tell Claude); `[config] settings in effect (59, version N):` and
  one line per setting (a changed one ends `(default …)`).
- Every change: `[config] <Key>: <old> → <new> (source: MCM|file|defaults)` — dragging a slider logs
  each step; Cancel logs the way back.
- Every game start and mission start: `[config] config.json re-read at <when>: no changes` or
  `N change(s), settings version N` (a hand edit applies at the next battle start, no restart).
- Mistakes are safe: a typo'd key → `[config] file problem: "AthleticsPoolFlor" is not a setting of this version - ignored (typo?)`
  (kept in the file under "Not recognised"); a bad number → `[config] could not read config.json at mission start (line …)`
  and the values stay; a missing key → `[config] not in the file, default used: <Key>` and it is written
  back; a file that does not parse is saved as `config.json.broken-<time>` before a fresh one replaces it.
- The two hooks, once per game: `[damage] damage model decorator registered over <the game's model> - …`
  (with War Sails over `NavalDLC.GameComponents.NavalAgentApplyDamageModel`, in a custom battle over
  `TaleWorlds.MountAndBlade.CustomAgentApplyDamageModel`) and `[speed] agent stat model decorator registered over … - …; tournament AI-level fix active over N base model(s)`.

### L2. Mission start and end, the summary header

- Start: `[config] config.json re-read at mission start: …`; `[mission] attached: AthleticsLogic (…)`;
  `[mission] start: scene <scene id>, field battle, mode …, combat type Combat, game …, agents so far …, mod ON`;
  `[mission] first tick: N agents active, …`; `[mission] deployment finished: N agents active, mode Battle`.
- End — every mission, towns and taverns too ("other mission"):
  ```
  [summary] ==== scene …, field battle - ended (mission end) after N s, result: player victory, mod ON ====
  [summary] agents built: N (N people incl. N heroes, N mounts)
  [summary] people removed: N killed, N knocked out, N fled, N other
  [summary] still on the field: …
  … the feature blocks (L3-L7) …
  [summary] errors logged during this mission: 0
  [summary] ==== end of summary ====
  ```
- `[log] N more verbose [<bucket>] lines were suppressed by the rate limit` — the flood guard, not a problem.

### L3. Damage randomness

Every hit that lands (anyone's) is multiplied by a fresh roll in `[1 − p, 1 + p × f]` (p = the spread,
f = the attacker's share of his peak line). A 0 hit stays 0; a real hit never drops below 1. Not rolled:
shield blocks (switch), falls, doors / siege engines / ships.
- `[damage] mission start: damage randomness ON, spread ±50% (a 50-damage hit lands for 25-75), melee on, ranged on, on mounts on, on shields off, upside follows the attacker's Athletics (DamageBonusFollowsAthletics) on - read live on every hit`
- Always, the first hit anyone lands: `[damage] first roll this mission - on the main thread: melee on a person: Imperial Recruit → Looter, Pitchfork, 18 → 23 (x1.26, attacker f 1.00 → up to x1.50)`
  (`NOT the main thread` → tell Claude; it is still safe).
- Verbose, per hit: `~[damage] melee on a person: <you> (you) → Looter, <weapon>, 34 → 41 (x1.21, attacker f 1.00 → up to x1.50)`;
  `~[damage] ranged on a person: …`; `~[damage] melee on a mount: … → horse <horse> of <rider>, …`;
  `~[damage] horse charge on a person: horse <horse> of <you> (you) → …, charge bump, 12 → 15 (x1.25, …)`;
  skips `~[damage] not rolled - <reason>: …` — `shield blocks (DamageRandomOnShields off)`, `spread 0 (DamageRandomPercent)`,
  `ranged (DamageRandomRanged off)`, `melee (DamageRandomMelee off)`, `on mounts (DamageRandomOnMounts off)`,
  `switched off (DamageRandomEnabled)`, `mod OFF (ModEnabled)`, `fall damage`, `objects (doors, siege engines, ships)`.
- Summary:
  ```
  [summary] damage rolls: 812 hits (melee 650, ranged 120, mounts 40, shields 2); factor min 0.50 / avg 1.002 / max 1.50; damage 21934 → 22011 (+0.4%), avg 27.1 per hit
  [summary] damage by kind: melee 650 x0.50..1.50 avg 1.004 (17000 → 17100) | ranged 120 … | mounts … | shields …
  [summary] damage dice, 10 equal slices from the lowest to the highest possible roll (even = fair): 81 79 85 80 77 84 83 80 79 84
  [summary] damage not rolled: 37 hits - fall damage 3, spread 0 (DamageRandomPercent) 4, shield blocks (DamageRandomOnShields off) 30
  [summary] damage upside by the attacker's Athletics (…): peak (f 1) N hits avg x1.0… max x1.50 upside 100% | f 0.5-1 … | f below 0.5 … | empty (f 0) … max x1.00 upside 0% | no pool (attacker not tracked) …; rolls above their allowed top: 0
  [summary] damage roll errors: none
  [summary] damage rolls ran on the main thread: all 812
  ```
  Proves: min ≥ 0.50 and max ≤ 1.50; avg ≈ 1.00 and before ≈ after (fair on average; a small fight
  wobbles a few percent); the dice slices roughly even; each switch you flipped under *not rolled*; the
  upside `max` falling row by row and **`rolls above their allowed top: 0`**; errors none. With the mod
  OFF: `damage while the mod was OFF - the game's own numbers, not rolled (factor 1.00): …`; a battle
  that flipped it also has `damage avg per hit: rolled (mod ON) … | mod OFF …`.

### L4. Athletics and speed

- Start: `[athletics] mission start: ON - pool = the Athletics skill x1.00, at least 50; full strength at 75% of the pool and above; cost per blow 10.0 / hero 7.5 / party leader 5.6 points, misses cost: yes; when empty: attacks at 20%, run x0.30, horses x1.00 (never slowed); damage upside follows Athletics: yes; wounds cap the pool: yes; refill after 3.0 s rest: … - read live`;
  `[athletics] party-leader rule: …` (custom battle: `no campaign (custom battle) - the side's general, or every hero of a side without one`);
  `[speed] stat model on top in this mission: ours, over <the game's model> - …` (**`WARNING: … not ours`** → another mod took the slot: tell Claude);
  `[athletics] party leader: …`, `[athletics] YOU: …` (C1), `[athletics] first tick: tracking N fighters`.
- Always: the `[athletics] YOU …` lines (C1, C3); once per battle the `[speed] first exhaustion this mission`,
  `[speed] first exhausted fighter leaves 0 after …` (`they stayed penalized: yes`), `[speed] first exhausted fighter back at full strength: … (x1.00 …)`
  — "did NOT take the asked factors" or "stayed penalized: NO" → tell Claude; with a horse slowed
  (*Horse speed when the rider is empty (x)* below 1.0): `[speed] first horse slowed this mission: …`.
- Verbose: `~[athletics] pool at spawn: …`, `~[athletics] hero: …`, `~[athletics] blow melee (on foot) / (mounted) / ranged / couched/braced …`
  (`- below full strength`, `- EXHAUSTED`), `~[athletics] exhausted: …`, `~[athletics] off empty: …`,
  `~[athletics] <name> is back to full …`, `~[athletics] health cap: <name> at 40% health - Athletics 90.0 → 52.0 of 130 (f 0.53)`,
  `~[speed] <name>: attacks x0.93, run x0.94 (f 0.92)`.
- Mid-battle changes: `[athletics] pool settings now: …; everyone keeps his share …`, `[speed] speed settings now: …`,
  `[athletics] AthleticsEnabled switched OFF / ON mid-mission: …`.
- Summary (numbers made up):
  ```
  [summary] Athletics settings at the end: ON - pool = the Athletics skill x1.00, at least 50; …
  [summary] Athletics pools (the Athletics skill x1.00, at least 50; settings at the end): 400 fighters - min 50 / avg 88.5 / max 130; 200 at the floor; you 90 (skill 90); party leaders: you 90, Arcor 80
  [summary] Athletics blows charged: 412 (melee swings 300, shots/throws 100, couched/braced hits 12, landed-only swings 0, landed-only shots 0) - by riders 60, on foot 352; Athletics spent 3890 points
  [summary] Athletics detection: melee releases seen 300 (mounted 50) | shots seen 100 (+0 extra projectiles of the same shot ignored) | ranged releases seen by the poll 98 | melee hits by fighters 280 (on foot 240, mounted 40): during a counted release 276, outside one 4 [in action: Other(0) 4]
  [summary] Athletics free (never charged): kicks 3, shield bashes 5, kick/bash hits 6, couched hits within one blow-length of the last 2, attacks while Athletics was off 0, …
  [summary] Athletics exhaustions (empty, f 0): 45 entered, 30 left; the peak zone: left 380 times (a blow took a fighter below his line), re-entered 150 times (by refill)
  [summary] Athletics fighter-time by f (the share of his peak line left): peak (f 1) 71.0%, f 0.5-1 16.0%, f below 0.5 9.0%, empty (f 0) 4.0% of 52000 fighter-seconds
  [summary] Athletics heroes: 2 flagged, 2 party leaders (you, Arcor); lowest a hero reached: Arcor 12.5 of 80
  [summary] Athletics you: skill 90 → pool 90; 25 blows, 1 exhaustion, lowest 0.0 of 90
  [summary] Athletics your formations at the end: 1 Infantry 61 ± 14 (38 men) f avg 0.71, 9 at full strength, health avg 74% | …
  [summary] Athletics health cap: 120 cuts (a wound pulled Athletics down to the health left), biggest 60.0 points, 2400 points in all
  [summary] Athletics regen: 9000 fighter-seconds refilling - at a walk or slower (effort up to 0.40) 7000 s at the full rate, faster 2000 s at avg x0.71; refills to the top: 38 to full, 12 to a wound's cap
  [summary] Athletics refill effort (speed ÷ current top speed), seconds per tenth (0-0.1 … 0.9-1, above 1): 5200 300 400 900 200 150 150 200 400 900 200, max 1.30
  [summary] speed updates: 900 recomputes asked (…; 0 held a tick by the per-tick budget), the decorator applied attack penalties in 1200 recomputes, run penalties in 1200, horse penalties in 0 (…)
  [summary] run speed check, on foot (÷ the fighter's own top speed when fresh), by f: peak (f 1) engine top x1.00 asked x1.00, moving p90 x0.95 … | … | empty (f 0) engine top x0.30 asked x0.30, … - the engine's top speed follows the curve
  [summary] run speed check, horses (…), by the rider's f: MountMinSpeedMultiplier 1.00 = horses never slow - … - unaffected, as asked
  [summary] walk vs run speeds (tune WalkEffortFraction, now 0.40): on foot walk limit avg 1.80 m/s (n 480), top avg 4.50 m/s (n 480) → walk/top 0.40; horses …
  [summary] Athletics tick cost: avg 0.120 ms, max 1.300 ms per tick over 5400 ticks; fighters polled avg 480, max 1020
  [summary] Athletics errors: none
  ```
  Proves: pools from the skill (min / avg / max, `at the floor` = the troops under skill 50; your pool =
  your skill); `whose skill could not be read` above 0 → tell Claude; the peak zone left and re-entered;
  **run speed `follows the curve`** (`does NOT follow` → tell Claude) and `moving p90` falling row by
  row; health-cap cuts after a real fight; regen by effort; **`walk vs run speeds`** — the ratio that
  tunes *Walking pace* (send it along: the setting should sit at, or a little above, walk/top on foot);
  `speed updates` in the hundreds or low thousands for a big battle and `held a tick` 0 or small;
  **`Athletics tick cost` avg well under 1 ms** in a 500+ battle (above 2 ms → tell Claude); detection:
  `outside one` small next to `during a counted release`, `shots seen` ≈ `ranged releases seen by the poll`;
  errors none.

### L5. Attack rate — the whole cycle follows m

A tired fighter's attack speed m drives three things: **the animations** (always: wind-up, swing,
draw, reload, throws ÷ m); **the AI's decisions** (*Tired AI attack less often*: its chance to attack,
riposte and loose × m, its aim ÷ m); **the pace hold** (*Tired AI keep a slower pace*: after each melee
swing a tired AI fighter on foot holds his next attack until his fresh cycle ÷ m has passed; never you,
never riders). Blocking is never slowed; the recoil after a blocked blow plays at the game's speed (the
known gap).
- `[rate] mission start: ON - animations x m always (…); AI decisions (AttackRateAiDecisions) on: …; pace hold (AttackRatePaceHold) on: …; blocking and the recoil after a block: untouched - read live; …`
- Once: `[rate] first slowed fighter this mission: …` (C2), `[rate] first pace hold this mission: …`
  and `[rate] first pace hold ended at … s after 0.40 s - time up; scripted flags now 0; …`.
- Verbose: `~[rate] pace hold: …`, `~[rate] pace hold ended: …`, `~[rate] pace hold not started: …`.
- Summary (numbers made up):
  ```
  [summary] attack rate, melee, AI, peak (f 1): wind-up 0.32 + held 0.08, swing 0.52 (clean, hit nothing 0.60), recoil after a block 0.40, pause 0.45 | cycle 1.40 s (n 900), m 1.00 - the fresh reference
  [summary] attack rate, melee, AI, f 0.5-1: wind-up 0.41 (x1.28) + …, pause 0.80 (x1.78) | cycle 1.85 s (n 300), m 0.78 → target 1.80 s: 103% - on target
  [summary] attack rate, melee, AI, empty (f 0): wind-up 1.60 (x5.00) + …, recoil after a block 0.41 (x1.02), pause 2.40 (x5.33) | cycle 6.60 s (n 40), m 0.20 → target 7.00 s: 94% - on target
  [summary] attack rate, melee, AI - verdict: ON TARGET in 3 of 3 tired bands (…)
  [summary] attack rate, melee, you, empty (f 0): … | cycle 4.60 s (n 12), m 0.20 → target 4.75 s: 97% - on target
  [summary] attack rate, ranged, AI, empty (f 0): draw 3.00 (x5.00) + aim 1.80 (x2.00), loose 0.20 (x1.00), reload 3.50 (x5.00), pause 0.90 (x3.00) | cycle 9.40 s (n 30), m 0.20 → target 13.50 s: 70% - too fast
  [summary] attack rate - left out: cycles whose two ends fell in different f bands …; longer than 4 s ÷ m …; readies that ended in no attack …; chained …; with a step back in them …
  [summary] attack rate - AI decisions (AttackRateAiDecisions on at the end): scaled in 1100 recomputes - …
  [summary] attack rate - pace hold (…): 500 holds, avg 0.85 s, max 3.10 s at avg m 0.45; by f: …
  [summary] attack rate - pace hold, not held: at full strength 2000, not needed … 150, the next attack already readied at the swing's end 60, … | not started by the tick: …
  [summary] attack rate - pace hold ends: time up 480, a swing started anyway 2 (must be about 0 - NoAttack holds swings), … | … | the next ready came avg 0.15 s after a hold ended (n 420) - near 0 = the hold set his rhythm
  [summary] attack rate - guard by f (…): peak (f 1) 45% (n 900) | f 0.5-1 47% (n 400) | f below 0.5 46% (n 150) | empty (f 0) 44% (n 60) | while held by the pace hold 52% (n 90)
  ```
  Proves: each group's **`verdict`** — `ON TARGET` = every tired band's cycle within ±15% of the peak's
  cycle ÷ m (`too fast` / `too slow` names the band); wind-up and swing (draw, reload) about 1/m = the
  engine honours the animation multipliers; `pause` growing = the decisions and / or the hold.
  AI rows too fast with the hold ON: `a swing started anyway` well above 0 → the engine ignores NoAttack
  (tell Claude); `the next attack already readied` large → the AI chains blows before the hold. Too fast
  with the hold OFF (C7 run 2) → keep the hold. Too slow with both on → the two stack; C7 run 3 tells
  which to keep. Ranged AI has no hold: `too fast` there → tell Claude. **Blocking untouched**: the
  `guard by f` rows within ~10 points of the peak row, `while held` not below it. You: your rows count
  only if you attacked as fast as you could.

### L6. Step back

- `[stepback] mission start: ON - …`; first tick `[stepback] this mission allows step backs while it is in battle mode (now: …)`
  — or `… is a tournament or arena fight (…) - no step backs here (vanilla AI)` / `… is a naval battle (moving decks) …`.
- The first one in full and its end (D1); verbose every start, end and refusal; switches (D3, B3).
- Summary — 8 lines:
  ```
  step back - technique: a scripted step - …; settings at the end: ON - …
  step back rolls after AI melee swings on foot, by f (the chance must be 0% at full strength and rise as f falls): peak (f 1) 900 swings, chance avg 0%, dice yes 0 (0%) | f 0.5-1 700 swings, chance avg 27%, dice yes 190 (27%) | f below 0.5 … | empty (f 0) 150 swings, chance avg 100%, dice yes 150 (100%); not rolled: you 40, riders 30, not a field battle (…) 0
  step back starts: dice yes 620 → started 480 (holding a line 200, charging 270, no formation 10), most at once 35; not started 140 (…reasons…)
  step back ends: 480 - completed (time up) 430, cut short 50 (left the field 30, formation order changed 15, …)
  step back moves: avg 1.20 m of 2.00 asked (min …, max …, reached the spot … of 480), lasted avg 1.40 s (n 480)
  step back facing (THE risk: a turned back) - at the start: facing his enemy … | mid-step (… sampled): facing his enemy …, side-on …, back turned …; moving away …, … | at the end (…): …
  step back guard: hits taken while stepping back N - blocked B (P%), landed … | everyone else on foot: … hits, blocked … (Q%) | swings started while stepping back 0 (0 expected: StepBackHoldAttacks is on)
  step back release check: N released through the engine - scripted movement still on right after 0 (must be 0), our flags (NoAttack, DoNotRun) cleared by hand 0 | at mission end: K were mid-step (released then), overdue (past their time) 0 (must be 0), scripted movement still on after that release 0 (must be 0)
  ```
  Proves: the `peak (f 1)` row ALWAYS `chance avg 0%, dice yes 0`, the rows below rising (≈ 100% × (1 − f));
  **`facing`: `back turned` near 0**, `facing his enemy` the big number; **every `(must be 0)` is 0**;
  `moves` 0.8–1.5 m of 2 is normal for a tired walker (about 0 = nobody moves); `guard` `P%` not well below
  `Q%`; `not started … engine did not take the scripted position N` large → the engine refuses the call.
  Must NOT happen (the `not rolled` / `not started` reasons): you, riders, archers shooting; *Shield
  wall*, *Square*, *Circle*; retreating or routing; ladders, siege towers and engines (`busy (the game's
  own check: …)`); a wall's edge or stairs (`spot not level`, `no straight way back`, `spot off the navmesh`);
  tournaments, arena, naval (`not a field battle (…)`).

### L7. The HUD — your bar and the orders strip

- Attach, first tick: `[hud] attached: player bar (…)` and `[hud] attached: orders strip (OrderStripView, movie TraxOrderStrip, prefab installed) - shown while ModEnabled, AthleticsEnabled and ShowInOrderMenu are on, … and while the orders menu is open`.
- Your bar: `layer created …`, `first values pushed …` (E1); the FIRST time of each colour per battle
  (C1 — the f at each must fit: blue between 0.75 and 1, yellow ≤ 0.75, orange ≤ 0.50, red ≤ 0.25);
  `EXHAUSTED shown`; `wounded` (C3); `layer removed at … - <reason>` (`ShowPlayerBar off`, `AthleticsEnabled off`,
  `ModEnabled off (the master switch)`, `the game's Hide battle UI is on`, `photo mode`, `no player agent on the field`,
  `not a fight (mission mode) - mode Conversation`); verbose `~[hud] player bar: colour yellow → orange at …`.
- Your bar outside a battle (F6, step 12 - its own rate bucket, so town walks never crowd out battle
  lines): `layer created at … (mode StartUp, outside a battle: a weapon drawn | your Athletics refilling, was hidden: …)`;
  `layer removed at … - outside a battle: no weapon drawn and your Athletics full` / `- outside a battle, and ShowPlayerBarOutsideBattles is off`
  / `- not a fight (mission mode) - mode Conversation`; `not shown at … - outside a battle, and this mission does not track your Athletics`
  (a mission without Athletics for you); verbose `~[hud] player bar: still shown at …, now outside a battle: your Athletics refilling (was: outside a battle: a weapon drawn)`.
  The attach line ends `; outside a battle too while ShowPlayerBarOutsideBattles is on (on now): in the walk-about mode (StartUp - towns, villages, the training field) …`.
- The strip: `layer created …` (later builds and removals by the menu closing go to the verbose log
  only), `first placement under the cards …` (the proof of the alignment: each cell's x = its card's x,
  its y = the card's y + height + 1 at UI scale 1), `values at … (open #N, …)` once per open,
  `the cards changed at … (open #N) - …` (a UI scale change, the gamepad row), `… - 1 cell lifted to the screen's bottom edge`.
- The fallback panel, with its reason: `[hud] orders strip: FALLBACK to the compact panel at … (open #N) - <why>; the panel lists your formations at the top of the screen (80 px down, 300 px wide) until the menu closes - the next open tries the cards again`
  — whys: `no MissionOrder layer on the screen and no other layer holds formation cards`, `N cards found - not whole sets of 8 …`,
  `two sets of cards drawn at once …`, `no card drawn (of 16 found) 0.60 s after the menu opened`,
  `… has 12 men but its card is not drawn`, `2 Archers's card counts 25 men, the formation has 20 for more than 1.0 s`.
- Summary:
  ```
  [summary] hud: player bar (movie TraxPlayerAthleticsBar) - on screen 312.4 s of 340.2 s (92%); layer built 2x, removed 2x (ShowPlayerBar off 1, mission end 1); hidden: ShowPlayerBar off 7.7 s, not a fight 20.1 s; 3120 refreshes; errors 0
  [summary] hud: player bar colours on screen - green 180.0 s (58%), blue 60.0 s (19%), yellow 40.0 s (13%), orange 20.0 s (6%), red 12.4 s (4%); 14 colour changes; exhausted shown 2x (8.2 s); wounded part shown 45.0 s (lowest usable 62% - the last 38% of the bar dark)
  [summary] hud: orders strip (movie TraxOrderStrip) - on screen 48.1 s of 340.2 s (14%); layer built 12x, removed 12x (orders menu closed 11, mission end 1); hidden: not a fight 20.1 s, orders menu closed 272.0 s; 480 refreshes; errors 0
  [summary] hud: orders strip - opened 12x: under the cards in 12, the compact panel in 0; technique: the live vanilla cards (layer MissionOrder: 16 cards); card layouts seen: 16 cards in 2 sets, set 1 drawn; cells placed 36 (lifted to the screen's edge 0), card changes 0, short mismatches 0 (under 1.0 s), card re-scans 0, values pushed 40; fallbacks: none
  ```
  Outside a battle (a town, the training field) the player bar's line also ends
  `outside a battle: shown 3x (weapon drawn 2, refilling 1), on screen 45.2 s; errors 0` (or `outside a battle: never shown`).
  Proves: *on screen* ≈ your time on the field in the fight; every *removed* has its reason; bar
  *refreshes* ≈ 10 a second on screen; the strip's *under the cards in* = *opened* and *fallbacks: none*;
  *short mismatches* small (a man fell between two updates); *errors 0*. A mission without a screen
  (rare): `[summary] hud: no views attached (…)`.

### L8. The log itself

- A verbose line: `~` before its tag. Only verbose lines are ever trimmed; every other line stays.
- Past *Log size limit (MB)* (8) the file is cut to about half and starts with one note:
  `[log] (log trimmed at <time>: N older verbose lines cut, up to <time of the newest cut>; every other line (load, settings, battle start and end, first-time events, summaries, errors) is kept)`.
  Only if the kept lines alone passed half the limit (dozens of battles over several game starts)
  would it say `…, and the other lines alone did not fit, so their oldest N went too, …`.
- The rate limit per kind of verbose line: 40 at once, then 20 a second; the `(+N similar lines
  suppressed)` notes and the `[log] N more … suppressed` lines at each battle's end say how much was
  dropped.

### Old section numbers

Earlier notes (AI_NOTES, RESEARCH, TASKS_TODO) point at the old numbering: §0-§1 → A and L1-L2;
§2 → C6, F2 and L3; §3 (3a-3m) → C and L4; 3n → C2, C7, F4 and L5; §4 → B; §5 → G; §6 → D and L6;
§7 → E1 and L7; §8 (8a-8g) → E2, E3 and L7.

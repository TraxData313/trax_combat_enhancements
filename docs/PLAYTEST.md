# Playtest — one session, start to finish

Anton plays this ONCE, after the build is finished, and hands Claude the log. Seven parts in
order, about **2 hours 50** (A 10 min, B 15, C 25, D 35, E 20, F 55, G 10; the optional bits add ~20). **Short on
time after step 16? D4 alone (the A/B of the new guard and backpedal, 20 min) answers the newest
questions.** Each part says what to do, what you
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
  - `[config] defaults: defaults.json (embedded in TraxCombat.Core.dll), 70 keys for 70 settings - every default read from it`
  - `[config] first run: created config.json with every default and a plain-words explanation beside each value`
  - `[config] settings in effect (70, version 0):` and 70 lines like `[config]   DamageRandomPercent = 50`
  - (If you kept your config.json from step 13 - YOUR file today, format 2 with the run floor 0.3 - instead:
    `[config] migrated config.json at startup: MinMoveSpeedMultiplier: 0.3 → 0.7 (format 2 → 3, the old default; step 14: an empty man runs at 70% of his pace - 0.3 was too slow)`,
    `[config] not in the file, default used: RegenRateNearFullPercent`,
    `added 1 missing setting(s) to config.json with their defaults` and `rewrote config.json as format 3 with the 1 migrated value(s)` -
    once (the settings dump then reads `MinMoveSpeedMultiplier = 0.7` and `RegenRateNearFullPercent = 50`); every other value of
    yours stays as it was; the next start reads format 3 and says nothing.
    An even older file (format 1, before step 13) also gets `AttackRateAiDecisions: true → false (format 1 → 3, …)` and
    `PlayerBarOffsetBottom: 54 → 30 (…)`.)
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
- Log: `[mcm] settings page registered at main menu (attempt 1): MCM 5.…, page "Trax Combat Enhancements", 78 settings in 10 groups, format "none" (config.json is the only store), Default preset = the mod's defaults (defaults.json); group "Defaults": buttons "Revert all to defaults" and "Save current values as a defaults file".`
  (`at retry (attempt 2)` is fine — MCM woke a moment after us; the first try is now always at the
  main menu, where MCM builds its services — step 12); the settings dump shows
  `VerboseLogging = true (default false)`.
- Broken: `[mcm] MCM … never became ready - gave up after 31 attempts …` with MCM enabled (the page
  is missing — send the `[mcm]` lines); a `not ready yet` line with no `registered` or `gave up` after it.

**A5. The MCM page.** Options → Mod Options → *Trax Combat Enhancements*.
- You see: the same 10 groups — *Master switch* (Mod enabled) on top, *Formation markers (hold ALT)*
  (step 20) just before *Advanced* at the bottom, and
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
hits, attack RATE and run speed fall in straight lines — at empty one attack where he made five, a
run at 70% of his pace (0.7 since step 14; it was 0.3). **PAUSE ONLY (step 13)**: every swing, thrust, draw, throw and reload plays at FULL speed
(no slow-mo); what slows is the **pause after each attack** — an attack of length D at attack speed m
is followed by D × (1/m − 1) in which no new attack can start (at empty: four attack-lengths). For
you the **Attack recovery bar** above your Athletics bar empties at the attack and fills over the
pause, the seconds inside it; pressing attack meanwhile does nothing and flashes it; holding attack
starts the next one the moment it is full. The AI waits out its pause the same way, guard up. Wounds
cap the bar. About 3 s after the last attack it refills: 60 s from empty to full standing or walking,
half that fast running flat out — **fast while the bar is low, slower as it fills** (step 14: near
full at half the speed near empty): half the bar in ~25 s, the peak line in ~41 s, the last quarter
~19 s. Blocking, parrying, kicks, moving, weapon switches: never held. **Kicks and shield bashes cost
Athletics (step 18)**: 3 for a soldier, 2.25 for a hero, ~1.7 for a party leader (you) — once each,
when they start, landed or not — but they never start a pause.

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
air (misses cost too), counting — click as fast as you can.
- You see: the number drops ~5.6 a swing; the first ~5 swings back to back, the bar green down to the
  white mark and the **Attack recovery bar** above it full and quiet; then the bar blue → yellow →
  orange → red and after every swing the recovery bar **empties and refills** (amber, the seconds
  inside it: `0.1 s`, `0.4 s`, … up to `≈ 3 s` at empty); **every swing itself plays at normal speed
  — no slow motion at all**. At ~16 the word reads *Exhausted* in red. Run: about 70% of your
  pace — slower, clearly, but not a crawl (step 14). Block: normal. Stop: about 3 s later the bar
  climbs — **quickly at first, then slower near the top** (step 14): ~25 s to half, ~41 s to green,
  60 s to full.
- Then empty yourself again and RUN flat out while it refills: about twice as long (the same curve).
- Tell Claude: is 70% the right run when empty? Does the refill curve feel right (fast back into the
  fight, slow to top off)? *Refill* → *Refill speed near full (%)* 100 = the old even refill to compare.
- Log (always):
  - `[rate] attached: your attack gate FIRST in the behaviour list (0 of N; behaviours pre-tick from the end, so it runs right after MissionMainAgentController at K) - …`
    (at the mission start - `could not attach your attack gate` → tell Claude) and, at your first pause,
    `[rate] your attack gate holds for the first time: it pre-ticks after MissionMainAgentController (gate 0, controller K of N …)` — **`WARNING: your attack gate's order is off` → tell Claude**
  - `[athletics] YOU: first attack pause this battle at … s - your melee attack (wind-up 0.28 + swing 0.55 = D 0.83 s) ended at attack speed x0.93 (f 0.91) → no new attack for 0.06 s = D x (1/m - 1), until … s; the hold began at your release's start (expected … s) …`
    — the pause must be D x (1/m − 1) (pauses under 0.1 s are skipped: the first ones may come a few
    swings below the line); later ones rate-limited: `[athletics] YOU: attack pause 1.20 s at … s (D 0.80 s at x0.40, melee) until … s`,
    `[athletics] YOU: attack pause ended at … s`
  - `[athletics] YOU: Athletics skill 90 → pool 90 (the skill x1.00, at least 50); full strength down to 68 (75%); a blow costs you 5.6 - about 5 blows at full strength, 16 to empty`
    — **the skill must match your character screen**
  - `[athletics] YOU dropped below full strength at … s: 61.9 of 90 (the line is 68) after 5 blows this mission - f 0.92: attacks x0.93, run x0.94, damage upside 92% of the full`
  - `[athletics] YOU are exhausted at … s: 0 of 90 after 16 blows this mission - attacks at 20% speed, run x0.70, no damage upside until you rest (…)`
  - `[athletics] YOU are off empty at …`,
    `[athletics] YOU are back at full strength at … s: 67.5 of 90 (the line is 68) - up from empty in 40.7 s of refill (40.7 s at a walk or slower; near full at 50% of the rate near empty (at a walk: half the bar in 25 s, the peak line in 41 s))`
    — standing or walking the "up from empty" time ≈ 40.7 s (step 14's curve; the even refill took 45 s); running, longer,
    `[athletics] YOU are back to full at … s: 0 → 90 of 90 in 60.0 s of refill (at a walk or slower 60.0 s, faster 0.0 s; avg rate x1.00; empty to full takes 60 s at rest, 120 s at a full run; near full at 50% of the rate near empty (…))`
    — after the run `… avg rate x0.5…`; **still 60.0 s at a walk** (the curve moves time from the top of the bar to the bottom, never adds any)
  - the colours: `[hud] player bar: BLUE for the first time this battle at … - f 0.97, …`, then
    `YELLOW`, `ORANGE`, `RED`, `[hud] player bar: EXHAUSTED shown at …`
  - the recovery bar: `[hud] recovery bar: first pause shown at … s - empty at your attack, now refilling over 0.40 s (D 0.82 s at attack speed x0.67), "0.4 s" inside it, counting down`
- Broken: your pool is not your Athletics skill; **a swing in slow motion** (the animations must stay
  normal — the summary's `animations asked x1.00`); no pause at empty; a swing that costs nothing;
  the number counting UP while you swing; a colour that disagrees with the fill (green below the mark);
  the recovery bar stuck empty or not filling.

**C1b. The pause — pressing early, holding, blocking, kicking (step 13).** Stay below the peak line
(orange or red is easiest) and try each, a few times:
1. **Press early**: tap attack while the recovery bar still fills. You see: nothing happens — no
   wind-up, no half swing — and the recovery bar **flashes twice**. Tap during your own swing too:
   no chained second blow (below the peak line), a flash.
2. **Hold to attack**: press attack right after a swing and keep it held. You see: the next wind-up
   starts by itself the moment the recovery bar is full.
3. **Block during the pause**: hold block while the bar fills (let an enemy swing at you). You see:
   you block normally the whole time. **Kick** (the kick key) and **switch weapons** during it: both
   work at once.
4. **Bow or javelins**: shoot; the next arrow is nocked as usual, but the draw waits until the bar
   is full (held, it draws the moment it fills). A bow you are already drawing is never cancelled.
5. **Only your own hands (step 17)**, if they come up: man a ballista or a mangonel right after a tired
   swing - it fires at the first click (the pause holds your weapon, not a machine); switch to RTS
   Camera's free camera while the bar refills - the bar is full at once: while the AI drives your hero
   he has no pause (REVIEW R25 - "For Anton"), and no "the input gate did not stop it" line comes of it.
- Log (always; the first of each in full, the rest rate-limited):
  - `[athletics] YOU: attack pressed at … s during your own attack (no chained blow below the peak line) - swallowed: the engine never saw it (no wind-up); the Attack recovery bar flashes; keep it held and the attack starts the moment the pause ends`
    and `[athletics] YOU: attack pressed early at … s with 0.62 s of your pause left - swallowed, bar flashed`
  - `[athletics] YOU: first attack pause ended at … s after … s (asked … s) - your attack button was held: the next attack starts now; presses swallowed during it: N`
  - `[athletics] YOU: your held attack button started the next attack at … s, 0.02 s after the pause ended (hold-to-attack: near 0 = it fires the moment the recovery bar is full)`
  - `[hud] recovery bar: first flash at … s - you pressed attack with … s of your pause left (2 pulses of 0.12 s)`
  - **Broken**: `[athletics] YOU: an attack began at … s while your pause held (…) - the input gate did not stop it` —
    a swing that started although the bar was not full (tell Claude); blocking or kicking not working during
    the pause; a stuck swing or a half wind-up after an early press.
- Summary: `attack rate - your timer (AttackRatePlayerTimer on at the end): N timers (melee …, ranged …), avg asked … s, max … s; your presses swallowed: A during your own attack (no chained blow), B during the countdown - the recovery bar flashed Cx; the button held through the end Dx, your attack began avg 0.0x s after (n D) - near 0 = hold-to-attack works; attacks that started while held anyway: 0 (must be 0 - the input gate missed them); …`
  — **`attacks that started while held anyway` must be 0**; `your attack began avg` near 0; and the
  `attack rate, melee, you, <band> - timer:` rows: `started before the timer ended: 0 (must be 0)`.

**C1c. Kicks and shield bashes cost Athletics (step 18).** Anywhere in the battle, at full strength
(green, recovery bar full and quiet):
1. **Kick 3 times** (at the air or an enemy). You see: the number drops **~1.7 each** — you are the
   general, a hero AND the party leader (3 × 0.75 × 0.75) — about 5 in all; a soldier pays 3, a
   companion 2.25. The recovery bar stays **full and quiet** after each kick: no pause.
2. **Shield bash** (attack while blocking, with a shield) 3 times, one landing on an enemy: the same
   ~1.7 each — the one that landed is charged once, not twice.
3. Below the peak line: a kick right after a swing works at once (never held); a bash waits for the
   recovery bar like any attack, and costs its ~1.7 when it goes.
4. *Athletics* → *Cost per kick or shield bash (points)* 0: a kick costs nothing any more (at once).
- Log (always; rate-limited): `[athletics] YOU: kick at … s cost 1.69 Athletics (3.00 x0.56 hero party leader): 90.0 → 88.3 of 90 (f 1.00 → 1.00) - the first this battle: a kick or a shield bash costs CostPerKickOrBash x your hero / party-leader multipliers, once, when it starts; it never starts your attack pause`,
  then `[athletics] YOU: shield bash at … s cost 1.69 Athletics …` — one line per kick or bash; a bash that landed has ONE line.
- Summary: `Athletics kicks/bashes charged N (… points; by riders 0): kicks K, shield bashes B, at their hit with no kick or bash seen H | seen starting: kicks K (channel 1 a, channel 0 b), shield bashes B (channel 1 c, channel 0 d); …`
  — **tell Claude which channel saw your kicks** (a or b: the game plays a kick on one of them - which one
  was not provable offline). `at their hit with no kick or bash seen` well above 0 → a kick or bash the poll
  missed (the hit still paid for it).
- Broken: a kick costing a full blow (5.6 for you) or nothing; a kick or a bash emptying the recovery bar;
  two lines for one bash.

**C2. Recruits run dry, legionaries keep going.** Let the lines meet; watch the front from close by.
- You see: recruits' swings stay quick, but after 2 swings they wait longer and longer between them
  and after ~5 they strike about once in four swing-lengths — between blows they stand, guard up,
  still blocking; legionaries keep their pace for ~4 swings and last ~13; exhausted men lag when a
  formation moves and fresh men overtake them. **No slow-motion swings anywhere.**
- Log:
  - verbose samples: `~[athletics] pool at spawn: Imperial Recruit - Athletics skill 20 → pool 50 (the floor); 2 blows at full strength, 5 to empty`,
    `~[athletics] blow melee (on foot): … - cost 10.0, 40.0 → 30.0 of 50 (f 1.00 → 0.80) - below full strength`
  - once: `[speed] first exhaustion this mission: <name> at … - properties before: … → after UpdateAgentProperties: … - the penalties are in the agent's properties`
    (with full-speed animations only the run value moves: `x1.00 / x1.00 / x1.00, run x0.…`)
  - once: `[rate] first slowed fighter this mission: <name> at … - attack speed x0.97 (f 0.96); animations (AttackAnimationMinPercent 100 → x1.00): swing … → … (each x1.00 as asked - full speed, no slow-mo); AI decisions (AttackRateAiDecisions off): … (unchanged, as asked); the timer (you: AttackRatePlayerTimer on, AI: AttackRatePaceHold on) comes after each attack …`
    — **`NOT … as asked - tell Claude`**
  - once: `[rate] first AI timer this mission: <name> at … s - his melee attack (D 0.80 s: wind-up + swing) ended at … s at attack speed x0.85 (f 0.81) → no new attack for 0.14 s = D x (1/m - 1), until … s; scripted flags 0 → 2 (NoAttack set: the engine took it)`
    — **`NoAttack NOT set` → tell Claude**; and `[rate] first AI timer ended at … s after … s - time up; …`
- The summary (appendix L4, L5) settles whether the engine honours it: the `attack rate, melee, AI,
  <band>` rows read `animations asked x1.00` and their wind-up / swing about `(x1.00)`; the
  `… - timer:` rows `started before the timer ended: 0`; `attack rate - AI timer ends: … an attack
  started anyway 0`; `run speed check, on foot … - the engine's top speed follows the curve`;
  `Athletics tick cost: avg … ms` (well under 1 ms).
- Feel: tired men swing as fast as ever but strike less often — longer gaps, guard up in between —
  while fresh men keep their pace. Say if it is too much or too little.

**C3. Wounds.** Take a few hits (stand in front of their archers).
- You see: the right end of your bar goes dark red-brown — the part your wounds hold; the refill
  stops at its edge; below about 75% health the bar never turns green again.
- Log: `[athletics] YOU are wounded at … s (62% health): Athletics capped at 55.8 of 90 (cut 11.2) - f now 0.83; full strength needs 68, out of reach until healed`
  and `[hud] player bar: wounded at … - the last 38% of the bar shown dark: usable 56 of 90 (health caps the bar); f can reach at most 0.83 now`.

**C4. Shoot.** Take a bow (or javelins) and shoot until empty; watch the two groups of archers.
- You see: every draw at normal speed; below the peak line the recovery bar refills after each shot
  (from the moment the next arrow is nocked), the next draw waits for it; tired archers' volleys thin
  out, their draws still quick.
- Summary: `attack rate, ranged, AI, <band> - timer: … (D avg 2.50 s at m … → asked avg … s = D x (1/m - 1)); … started before the timer ended: 0 (must be 0)`
  (a ranged D is draw + loose + reload); `attack rate, ranged, AI - verdict: …`; `attack rate - AI timer
  (…): N holds (melee …, ranged R, …)` with R above 0; `Athletics detection: … shots seen N … | ranged releases seen by the poll M` (N ≈ M).

**C5. Ride.** Mount, swing from the saddle; couch a lance if you have one; then ride 30 s without
attacking.
- You see: the same curve on horseback — the same pause and recovery bar after a swing from the
  saddle; riding itself never drains you; horses keep their speed; tired enemy riders (horse archers
  too) attack less often.
- Summary: `Athletics detection: … melee hits by fighters H (…): during a counted release A, outside one B` — **B small next to A**;
  `attack rate - AI timer (…): N holds (…, mounted M)` — riders are held too (step 13).

**C6. Change it mid-battle (hot swap).** Escape → Mod Options → Trax Combat Enhancements; every change
applies on the first frame back.
- *Tired fighters* → *Attack speed when empty (%)* 20 → 50: empty men pause one attack-length instead
  of four. Log `[config] ExhaustedAttackSpeedPercent: 20 → 50 (source: MCM)`, then
  `[speed] speed settings now: when empty attacks at 50%, run x0.70, horses x1.00; full strength at 75% of the pool - N fighters get new speeds …`. Back to 20.
- *Refill* → *Refill speed near full (%)* 50 → 100 while your bar refills (step 14): from then on it
  climbs at one even speed (the old refill). Log `[config] RegenRateNearFullPercent: 50 → 100 (source: MCM)`;
  your next `YOU are back to full …` line ends `the same rate all the way (RegenRateNearFullPercent 100)`. Back to 50.
- *Tired fighters* → *Your attacks wait out the pause* off (while your bar refills): the recovery bar
  goes, you attack freely at once. Log `[rate] AttackRatePlayerTimer switched OFF mid-mission at … s: your attacks are no longer held …`
  and `[athletics] YOU: your attack pause released at … s - switched off (…): attack at once`. Back on.
- *Tired fighters* → *Slowest attack animation (%)* 100 → 50: tired swings slow down again (a little
  slow-mo on top of the pause) - log `[rate] AttackAnimationMinPercent now 50 at … s: N tired fighters get their attack animations at x max(m, 0.50) …`. Back to 100: normal speed.
- *Your Athletics bar* → *Attack recovery bar* off / on: it goes and comes back (`[hud] recovery bar: layer removed at … - ShowAttackRecoveryBar off`).
- *Damage randomness* → *Spread (± %)* 50 → 0: the same blow deals the same number. Log per hit
  `~[damage] not rolled - spread 0 (DamageRandomPercent): melee on a person: …`. Back to 50: the very
  next hit rolls again.
- *Athletics* → *Athletics* off: every slow fighter is normal at once, your bar and the recovery bar
  go, every pause (yours and the AI's) ends at once. Log
  `[athletics] AthleticsEnabled switched OFF mid-mission: N fighters back to full, M speed penalties lifted …`,
  `[rate] AthleticsEnabled switched OFF mid-mission at … s: N held fighters may attack again at once`
  (and your `YOU: your attack pause released …` if one ran); on again `… switched ON mid-mission: everyone starts full …`. Then finish the battle.

**C7 (optional, +10 min). The attack-rate A/B.** Three short runs of C's setup, flipping BETWEEN
battles: (1) the defaults (the AI's pause on, *Tired AI also decide to attack less* off); (2) *Tired AI
also decide to attack less* ON as well (both stacked - step 5e's log read "too slow" like this);
(3) *Tired AI wait out the pause* off (the AI not held - its rate then barely drops). Compare each
summary's `attack rate, melee, AI - verdict` line and tell Claude which read closest to ON TARGET —
and which FELT right.

---

## D. Tired men step back — and keep their guard up (35 min)

After a melee swing, an AI fighter on foot below full strength may step back ~2 m straight away from
the man he fights, facing him, guard up, no swings — until he has covered the 2 m or 1.5 s have passed
— then his formation takes him back. Chance = 100% × (1 − f): never at full strength, every swing at
empty. Never you, never riders, never after a shot; field battles only; not from a shield wall, square
or circle. **Step 16** (the playtest of 2026-09-27 showed ~80% turning their backs and tired men blocking
almost nothing): he now **walks backwards** (like you holding S), and a tired man waiting out his pause
keeps his **guard really up** — both through the AI's own controls (one small component per held man,
the hook RTS Camera uses too). His pause also survives a step back now. The old ways stay one switch
away each for the A/B in D4: *Tired AI keep their guard up (new way)* and *Step back = walk backwards
(new way)*.

**D1. See it.** Custom battle, infantry against infantry (60 v 60, no archers, a flat map). Let the
lines meet; after a minute watch the front from close by (or from the side).
- You see: a man who has swung a few times backs off a step or two **walking backwards, still facing
  the enemy, shield up**, and walks back into the line a moment later; the men beside him keep fighting;
  it happens more as the fight wears on; the line keeps its shape. Tired men who are not stepping back
  stand with their **guard up** between their (rarer) attacks - shields raised, blocks - instead of
  standing open.
- Log:
  - `[stepback] mission start: ON - after a melee swing an AI fighter on foot steps back with chance 100% x (1 - f) … at most 50 at once; a backpedal (StepBackBackpedal on: a backwards input, facing his enemy, until the distance is covered) - read live; technique: a backpedal - …`
  - `[stepback] first step back this mission: <name> at 63.2 s (f 0.40, …) - from (…) to (…) (2.00 m straight away from <enemy>, …); facing before: 12° off his enemy; formation 1 Infantry (…); technique: a BACKPEDAL through his own input (our component added, the engine's input callback …; the line away from him (…) in his own frame now (0.02, -1.00) - (0, -1) = straight back; his attacks held (only the attack bits out); scripted flags none (none of ours))`
  - `[stepback] first step back ended (arrived (StepBackDistance covered)) after 1.1 s: now at (…), moved 2.00 m …, facing his enemy (8° off, 3.6 m from him); mid-step facing his enemy (…); every 0.25 s (metres back, facing): +0.3 s 0.45 m, 5° off, 1.60 m/s away | +0.5 s 0.90 m, 7° off, … | …; hits taken 1 (blocked 1), swings 0; the engine's first call: movement bits … → …, input vector (…) → (0.02, -1.00); …; calls so far N; the backpedal input stopped - nothing of ours left in the engine …`
  - `[rate] first AI timer this mission: <name> at … - his melee attack (D 0.82 s …) … → no new attack for 1.30 s = D x (1/m - 1), until … s; technique: BY INPUT - our component added, …; only the attack bits are taken out of his own input while it runs, a guard raised when he wants to attack; …`
    and `[rate] first AI timer ended at … - time up; the input hook: the engine's first call: movement bits … ; an attack wish while held: AttackDown → DefendDown (the attack taken out, a guard raised); calls so far N; melee hits taken while held H (blocked B); …`
  - verbose: `~[stepback] step back: …`, `~[stepback] step back ended (…): …`, `~[stepback] step back not started: … - <reason>`
  - **A WARNING to stop for**: `[rate] WARNING: <name> was held by input for … s and the engine never called our input hook - …`
    = the new way does nothing in this game: switch both new ways off (D4's run 2) and tell Claude.

**D2. Force it.** *Athletics* → *Smallest bar (points)* 50 → 10 (recruits empty after two blows).
- You see: most tired men step back after every swing — the front "breathes" back and forth but
  keeps its shape. Set it back to 50.

**D3. Off mid-battle.** *Tired fighters step back* → *Tired fighters step back* off → Done.
- You see: anyone stepping back walks straight back into his formation; nobody steps back any more;
  on again, they start from their next swing.
- Log: `[stepback] StepBackEnabled switched OFF mid-mission at …: N fighters stepping back released to their formations at once`.

**D4. The A/B — the new ways against the old (step 16, ~20 min).** The same custom battle twice, as
alike as you can make them: the same map, the same two armies (e.g. 120 v 120 infantry, tier 1-2, no
archers - the battle where tired men mattered most), you as general, **charge and then leave the
armies alone** (no orders after the charge - the two runs must differ only by the switches). Note
the clock time at each start.
1. **Run 1 - the new ways** (the defaults): MCM → *Tired fighters* → *Tired AI keep their guard up (new
   way)* ON, *Held AI raise their guard* ON; *Tired fighters step back* → *Step back = walk backwards (new
   way)* ON. Fight it out (or 5 minutes of melee).
2. **Run 2 - the old ways** (steps 13-15): before the battle switch *Tired AI keep their guard up* OFF and
   *Step back = walk backwards* OFF → Done. Fight the same battle the same way. Switch both back ON after.
- **Look for** (run 1 against run 2):
  - **Tired men raise their shields** between attacks - in run 2 they stood open while waiting.
  - **The step back faces the enemy** - a short backwards shuffle, shield toward the enemy; in run 2
    most turned round and walked off.
  - **Fights last longer**: the melee (first contact → one side broken) takes longer in run 1; the
    summary header's `after N s` and `people removed` give it, and the landed hits per man removed.
  - Empty men attack rarely even when they step back (in run 2 they swung again ~1.5 s after each step).
- **The lines that settle it** (each run's `[summary]` block; the header names the run:
  `==== scene …, mod ON, AI holds: Input (the AI's own input: guard up, backpedal) ====` vs
  `…, AI holds: Legacy (NoAttack + the scripted walk) ====`):
  - `AI holds - GUARD (…): held by the timer X% (n), stepping back Y% (n) (…), everyone else Z% (n) - of
    them tired … , at full strength … - gap to everyone else: held -a points, stepping back -b points`
    → **run 1: X and Y close to Z (a gap of 10 points or less); run 2: far below** (the 240v240 read 2% and
    5% against 33%).
  - `step back facing (THE risk: a turned back) - … | mid-step (…): facing his enemy …, side-on …, back
    turned … | … | every 0.25 s (N samples): …; step backs with the back turned at ANY sample K of M (P%)
    (must be about 0)` → **run 1: back turned about 0, P% under 5%; run 2: ~80%.**
  - `step back ends: N - completed (time up) …, arrived (StepBackDistance covered) A, cut short …` and
    `step back moves: avg X m of 2.00 asked (…) … - about V m/s` → run 1: X near 2 or `arrived` a big share;
    run 2: ~0.6 m, 2 arrivals in thousands.
  - `attack rate, melee, AI, empty (f 0) - timer: … the timer's floor D/m avg F s - the cycle vs it: P% (n …),
    with a step back inside Q%, without R% (at least ~100% = held as the spec asks)` → **run 1: P and Q at
    100% or above** (in the old log the empty band's cycle was 2.2 s against a floor of ~4.3 s: ~50%). The
    band line's `→ target … s: …% - too fast / on target` is the older yardstick (the fresh cycle ÷ m) -
    report it, it may still read "too fast" (AI_NOTES "Step 16" #7 says why).
  - `AI holds - the timer survives a step back: holds that overlapped a step back N (…) | AI attacks that
    started while a hold or a step back held him anyway K (must be about 0)`.
  - `AI holds - the input hook (…): M men hooked (…), the callback already on for C of them (…RTS Camera…)
    …; calls while held … (about R a second per held man); the attack bits taken out in … calls (a guard
    raised in …, his own guard kept in …, a ready cancelled in …), a backpedal written in … calls; …; holds /
    backpedals the engine never called us during 0 / 0 (must be 0 …); errors 0`.
  - `attack rate - AI timer ends: time up …, an attack started anyway K (must be about 0 - the hold stops
    attacks) … | the next ready came avg T s after a hold ended … ` → run 1: K about 0; T well under
    run 2's 1-3 s.
- Tell Claude which run FELT better, and whether the backpedal looked natural (speed, distance).

**Broken — tell Claude** (the summary's step-back lines, appendix L6 / L6b, carry the proof):
1. **Men turn their backs** to walk away — THE risk. With the new way this should be gone; if not
   (`back turned at ANY sample` high in run 1), set *Chance when empty (%)* to 0.
2. **Men stuck** behind the line or frozen — every `(must be 0)` in the `release check` line must be 0;
   with the new way a man frozen walking backwards = tell Claude the clock time.
3. **Formations fall apart** — try *Most at once (whole battle)* 10–20 and say which felt right.
4. **They drop their guard** while stepping back or waiting (the `GUARD` line) — try *Held AI raise
   their guard* off, and say which read better.
5. `swings started while stepping back` above 0 with *No swings…* on.
6. The `[rate] WARNING: … the engine never called our input hook` line, or `errors` above 0 in the hook's
   line — switch both new ways off and tell Claude.
7. Men walking backwards off a wall walk or into a ditch in a siege (F2) — the `edge ahead` ends should
   stop that; tell Claude where.

---

## E. Your Athletics bar, the orders-menu strip and the ALT markers (20 min)

**Your bar** — bottom right, one row under the vanilla health bar (and the horse bar when you ride),
its right end lined up with the health bar's fill: the word *Athletics*, the number `132 / 180`
(left / your whole bar; it reads 0 only when truly empty), and a slim bar: the fill coloured by f
(green at or above the peak line, blue just below, yellow ≤ 75% of the line, orange ≤ 50%, red ≤ 25%),
a white mark at the peak line, the part your wounds hold dark red-brown, the rest dark grey. Empty:
*Exhausted*, word, number and frame red. **Just above it (step 13) the Attack recovery bar**: the words
*Attack recovery* and a slim bar — full and steel-grey when you may attack, EMPTY when you attack
below the peak line, then filling (amber) over your pause with the seconds left inside it; it
flashes white twice when you press attack too early. Since step 13 your Athletics row sits a little
lower (30 px from the bottom) to make room.
```
                                     [♥ ████████████ health ████████████ ]
       Attack recovery              [████████████████████████████████████]     full: you may attack
             Athletics  180 / 180   [██████████████████████████|███████]     all green, mark at 3/4

       Attack recovery              [██████████░░░░ 1.3 s ░░░░░░░░░░░░░░░]     filling: 1.3 s of your pause left
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
- You see: the two rows (Attack recovery above, Athletics below) under the health bar (and the horse
  bar), not overlapping them or each other, not cut off; they scale with the UI scale like the health
  bar; they go and come back together with each switch (the recovery bar also with *Attack recovery
  bar* and *Your attacks wait out the pause*).
- Log: `[hud] attached: player bar (PlayerAthleticsView, movie TraxPlayerAthleticsBar, prefab installed) - …`;
  `[hud] attached: recovery bar (AttackRecoveryView, movie TraxAttackRecoveryBar, prefab installed) - shown while ModEnabled, AthleticsEnabled and ShowAttackRecoveryBar are on, …, you are on the field and your Athletics bar (ShowPlayerBar) and your pause (AttackRatePlayerTimer) are on; outside a battle too …`;
  `[hud] player bar: layer created at 31.2 s (mode Battle, was hidden: not a fight (mission mode)) - movie TraxPlayerAthleticsBar loaded OK (13 widgets)`;
  `[hud] recovery bar: layer created at 31.2 s (…) - movie TraxAttackRecoveryBar loaded OK (9 widgets)`;
  `[hud] player bar: first values pushed at … - 90 / 90, fill 1.00, …, colour green (peak zone - full strength), …`;
  `[hud] recovery bar: first values pushed at … - full (no pause running); bar 205 x 14 px, 62 px from the right edge and 54 px from the bottom (your Athletics bar's row 30 + 24; …); flash on an early attack on`;
  `[hud] player bar: layer removed at … - the game's Hide battle UI is on` / `- no player agent on the field` / `- ShowPlayerBar off`
  (and the same `[hud] recovery bar: layer removed at …` lines; `- your Athletics bar (ShowPlayerBar) or your pause (AttackRatePlayerTimer) is off`).
- If it sits wrong: move it live with *Advanced* → *Your bar: length / thickness / from the right
  edge / from the bottom edge (px)* (the recovery bar moves with it) and *Recovery bar: length /
  thickness / above your bar (px)* until it looks right; tell Claude the numbers and your resolution
  and UI scale.
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

**E4 (5 min). Hold ALT — the numbers under the formation markers (step 20, Anton's ask).** In a custom
battle with several formations on both sides (RTS Camera on), **hold Left Alt** (the game's "show
indicators" key) before the lines meet, then again after a while of fighting.
```
          57             the game's marker: the troop count,
         [⚔]             the formation icon (your colour / an ally's / the enemy's),
         » 45            the distance (the footprint icon and the number)
   72% ± 8   HP 81%      ← ours: Athletics ± spread in the colour of the men's strength, their health
     [████▒▒│░░]         ← ours: the same slim bar as the strip (fill, ± band, peak tick)
```
- You see: under EVERY marker the game draws — yours, your allies' and the enemy's — our two numbers
  and the bar, centred under the distance number, following the marker as you move the camera (no lag,
  no drift); none under a marker the game hides (behind you, very close, a formation wiped out). They
  come and go exactly with the markers: hold ALT → both; let go → both go; **open the orders menu → the
  markers show, and so do ours** (the game shows its markers with the menu). After fighting: the
  front formations' numbers lower and yellow / orange / red, the fresh reserves green; the enemy's too.
- With RTS Camera: switch to its free camera and hold ALT again — the numbers stay under the markers
  wherever you fly; your own formation's marker vanishes when you are alone in it (RTS Camera hides it)
  and so do ours.
- Switch mid-battle (Escape → Mod Options): *Formation markers (hold ALT)* → *Enemy formations too* off →
  the enemy's numbers go at once, yours stay; on again. *Numbers under the formation markers* off → all
  go; on again. *Orders menu strip* → *Show average health* off → the `HP` goes here too. *Advanced* →
  *Marker numbers: text size / gap under the marker / bar length / bar thickness (px)* move them live
  (bar thickness 0 = no bar) — tell Claude the numbers that look right.
- Log (appendix L7): `[hud] attached: ALT markers (AltMarkerView, movie TraxAltMarkers, prefab installed) - shown while ModEnabled, AthleticsEnabled and ShowAltMarkerStats are on, the game's Hide battle UI and photo mode are off, in a fight (battle, duel, tournament or stealth mode) and while the game shows its formation markers (ALT held or the orders menu open - read live from its own marker layer)`;
  the first ALT: `[hud] ALT markers: layer created at …` then **`[hud] ALT markers: first shown at … (show #1) - technique: the game's own formation markers read live (their points and their widgets' sizes) (layer MissionFormationMarker, movie FormationMarker: 6 markers, 6 marker widgets); screen 1920 x 1080 px, UI scale 1.00; 6 markers (yours 3, allies 0, enemy 3): yours 1 Infantry (41 men) at (960, 402) 60 x 108, 85 m | …; labels (…): yours 1 Infantry at (960, 458) | …; no label: enemy 3 Cavalry - behind the camera`**
  — THE proof of the alignment: each label's centre x = its marker's x, its top y = the marker's y + half its height + 2 at UI scale 1;
  `[hud] ALT markers: values at … (show #1): yours 1 Infantry 72% ± 8 HP 81% (40 men, f 0.93) | … | enemy 1 Infantry 64% ± 12 HP 95% (…) - ± is 1.00 std, health on, enemy on`.
  Later ALT presses are quiet (verbose `~[hud] ALT markers: layer created …`).
- Summary: `[summary] hud: ALT markers - shown 14x, on screen 32.5 s; technique: the game's own formation markers read live (…); formations labelled: yours 3, allies 0, enemy 3 (most at once 6), …; fallbacks: none; errors 0`.

**Broken — tell Claude**: no bar at all (read the `[hud]` lines in order: no `attached:` → look for
`[error] hud.attach`; `prefab NOT FOUND` → the GUI folder did not install; `movie … FAILED to load`);
the bar, the recovery bar or a cell in the wrong place, overlapping vanilla UI or each other, or cut off; the recovery bar
stuck empty or never flashing; a colour that disagrees with
the fill or its `for the first time` line's f; a wrong number (not your pool, counting up, 0 with fill
left); the wounded part missing; staying on screen when it should hide, or not coming back; a cell
under the wrong card or a card with men and no cell; the **compact panel with *Strip under the cards*
on** (send the `FALLBACK` line — appendix L7); the mouse snagging on our layer, or RTS Camera's card
clicks not working while the strip is up; `[hud] … DISABLED for the rest of this battle`. **E4**: numbers
with no marker above them, a marker without numbers that should have them (a formation with men,
in front of you), numbers that trail or jump when the camera moves, numbers staying after ALT is
released, any `[hud] ALT markers: FALLBACK …` or `a marker without its widget` line, or a summary whose
technique is not *read live*.

---

## F. Campaign — a field battle, a siege, a tournament, two hideouts, the training field (55 min)

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

**F5 (20 min). Two hideouts — the boss fight is a fresh start (step 19).** Anton's ask: when the boss
comes out with his few friends, your side's Athletics is full again — duel or all against all —
"because they will come fresh and we will be tired". Clear a hideout's first fight **tired**: swing a
lot at the end so your bar is yellow / orange (or red) and your men are worn down. Then the cutscene
(the boss and his men walk out) and the talk. **Hideout 1: "Very well."** (the duel). **Hideout 2: "I
don't fight duels with brigands."** (everyone fights).
- You see: the moment the talk closes and the fight begins, **your bar jumps to full** (green, the
  number at the top) — or, wounded, to the dark part your wounds hold (the number stops at your health
  left: that is right). The Attack recovery bar is full and quiet: your first swing at the boss has no
  pause. In the DUEL your men stand aside and are NOT refilled (only you); in the BATTLE your men fight
  fresh too — no step backs, no long waits between their first swings. The boss and his men are fresh
  either way (the game spawns them in the cutscene). The stealth phase before costs Athletics as
  usual (the bar shows); the boss fights in battle mode and may step back when tired - say if that
  feels wrong.
- *Refill* → *Hideout boss fight: your side starts fresh* off (before a hideout's talk): the bar does
  NOT jump; the log line says `nobody refilled - HideoutBossFightRefill is off`.
- Log (always): at the first tick `[athletics] hideout mission (HideoutMissionController): watching for the boss fight's start (the game's "Win the Duel" / "Win the Fight" objective) - then the player's side starts fresh (HideoutBossFightRefill on, read then)`
  (the stealth hideout says `HideoutAmbushMissionController`); at the cutscene `[athletics] hideout: the boss intro began at … s (the cutscene) - the boss and his men spawn now, fresh; …`;
  at the fight's start:
  - duel: `[athletics] hideout boss fight (duel): refilled 1 of the player's side (you 38.0 → 120.0 of 120) at … s - in a duel only you: your men stand aside; 1 on the player's side, 0 already full, 0 held below full by their wounds (to the health they have left), 0 were empty; +82.0 Athletics in all; released: your attack pause no, AI pauses 0 (+0 queued), step backs 0 (+0 queued); the boss's side: 1 fighter, all fresh (at full - spawned for this fight); standing aside: 9 (not refilled)`
  - battle: `[athletics] hideout boss fight (battle): refilled 8 of the player's side (you … → … of …) at … s - you and your men still standing; 9 on the player's side, 1 already full, 2 held below full by their wounds …; the boss's side: 6 fighters, all fresh (at full - spawned for this fight)`
    (your pause, AI pauses or step backs still running are released there too - normally 0: the
    cutscene and the talk outlast them); if your pause was running: `[athletics] YOU: your attack pause released at … s - a fresh start (the hideout boss fight began): attack at once`.
  - the summary: `[summary] hideout boss phase (HideoutMissionController): the boss intro at … s, the fight began at … s as a DUEL - a fresh start: 1 of the player's side refilled (…; you 38.0 → 120.0 of 120); the boss's side: 1 fighter, all fresh (at full - spawned for this fight)`
    (the battle: `as a BATTLE (men to men)`).
- Broken — tell Claude: no `hideout boss fight` line in a hideout where you fought the boss (the summary
  then says `the boss intro played at … s, but the start of the boss fight was NEVER SEEN - … the refill hook never fired: tell Claude`);
  `duel or battle` in the line (the game's objective name was not the one expected - the refill still
  ran); `the boss's side: … - NOT all fresh (tell Claude)`; `the refill FAILED`; in the duel your men
  refilled (`refilled` above 1), in the battle your men not refilled; the bar not jumping.

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
  `[config] reverted all 78 settings to their defaults (…): N changed, applied live`.

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
   at 3 (step 18) - too cheap, too dear?; Athletics in tournaments and the arena (on now)?; which attack-rate
   switch felt right (C7); the balance — recruits empty after 5 swings, one attack in five at empty, a
   run at 0.7 (step 14), ~41 s from empty to full strength at rest along the refill curve (half the
   bar in ~25 s): too harsh, too soft, right?
6. Optional: screenshots of the bar and the strip.

---

## Appendix — the log, line by line

### L1. Load, settings, MCM

- Load: `[load]` lines at every game start (A1), incl. `[load] module: <Id>` - which copy runs (dev or
  release); `[compat]` RBM or not; `[compat]` two copies enabled and the other stood down (A6 - only
  when both are on); `[mcm]` registered or not (A1, A4) — step 12: at most ONE `not ready yet` line,
  followed by `registered at retry (attempt N)` or one `never became ready - gave up after 31 attempts …`;
  with MCM's module off but its DLL carried by another mod, one `MCM's module is not enabled - no settings page; config.json only.`
  and nothing more; `[config] defaults: … 70 keys for 70 settings` (any `defaults.json PROBLEM:` under it →
  that setting runs on a fallback — tell Claude); `[config] settings in effect (70, version N):` and
  one line per setting (a changed one ends `(default …)`). An old config.json is migrated once: format 2
  (step 13's) `[config] migrated config.json at …: MinMoveSpeedMultiplier: 0.3 → 0.7 (format 2 → 3, the old default; step 14: …)`;
  format 1 (before step 13) also `AttackRateAiDecisions: true → false (format 1 → 3, the old default; …)`
  and `PlayerBarOffsetBottom: 54 → 30`; then `rewrote config.json as format 3 …`.
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

- Start: `[athletics] mission start: ON - pool = the Athletics skill x1.00, at least 50; full strength at 75% of the pool and above; cost per blow 10.0 / hero 7.5 / party leader 5.6 points; a kick or shield bash 3.00 / hero 2.25 / party leader 1.69 points, misses cost: yes; when empty: attacks at 20%, run x0.70, horses x1.00 (never slowed); damage upside follows Athletics: yes; wounds cap the pool: yes; refill after 3.0 s rest: empty to full in 60 s at a walk or slower (up to 0.40 of top speed), x0.50 at a full run, near full at 50% of the rate near empty (at a walk: half the bar in 25 s, the peak line in 41 s) - read live`;
  `[athletics] party-leader rule: …` (custom battle: `no campaign (custom battle) - the side's general, or every hero of a side without one`);
  `[speed] stat model on top in this mission: ours, over <the game's model> - …` (**`WARNING: … not ours`** → another mod took the slot: tell Claude);
  `[athletics] party leader: …`, `[athletics] YOU: …` (C1), `[athletics] first tick: tracking N fighters`.
- Always: the `[athletics] YOU …` lines (C1, C3); once per battle the `[speed] first exhaustion this mission`,
  `[speed] first exhausted fighter leaves 0 after …` (`they stayed penalized: yes`), `[speed] first exhausted fighter back at full strength: … (x1.00 …)`
  — "did NOT take the asked factors" or "stayed penalized: NO" → tell Claude; with a horse slowed
  (*Horse speed when the rider is empty (x)* below 1.0): `[speed] first horse slowed this mission: …`.
- Verbose: `~[athletics] pool at spawn: …`, `~[athletics] hero: …`, `~[athletics] blow melee (on foot) / (mounted) / ranged / couched/braced …`
  (`- below full strength`, `- EXHAUSTED`), `~[athletics] kick (on foot): <name> - cost 3.0, 50.0 → 47.0 of 50 (f 1.00 → 1.00)`
  / `shield bash` / `kick/bash at its hit` (step 18), `~[athletics] exhausted: …`, `~[athletics] off empty: …`,
  `~[athletics] <name> is back to full …`, `~[athletics] health cap: <name> at 40% health - Athletics 90.0 → 52.0 of 130 (f 0.53)`,
  `~[speed] <name>: attacks x0.93, run x0.94 (f 0.92)`.
- Mid-battle changes: `[athletics] pool settings now: …; everyone keeps his share …`, `[speed] speed settings now: …`,
  `[athletics] AthleticsEnabled switched OFF / ON mid-mission: …`.
- Hideouts (step 19, F5): `[athletics] hideout mission (…): watching for the boss fight's start …`, `[athletics] hideout: the boss intro began at …`,
  `[athletics] hideout boss fight (duel|battle): refilled N of the player's side (you X → Y of P) at … s - …` (off: `… at … s: nobody refilled - …`;
  broken: `the refill FAILED (hideout.refill) - nothing refilled, …` + one `[error] hideout.refill`), and the summary's
  `[summary] hideout boss phase (…): …` - in hideouts only; `NEVER SEEN` or `NOT all fresh` in it → tell Claude. A released step back
  or pause is named `a fresh start (hideout boss fight) N` in the step-back and attack-rate summary lines (only when N > 0).
- Summary (numbers made up):
  ```
  [summary] Athletics settings at the end: ON - pool = the Athletics skill x1.00, at least 50; …
  [summary] Athletics pools (the Athletics skill x1.00, at least 50; settings at the end): 400 fighters - min 50 / avg 88.5 / max 130; 200 at the floor; you 90 (skill 90); party leaders: you 90, Arcor 80
  [summary] Athletics blows charged: 412 (melee swings 300, shots/throws 100, couched/braced hits 12, landed-only swings 0, landed-only shots 0) - by riders 60, on foot 352; + kicks/bashes 8 (not blows - their own line); Athletics spent 3914 points (kicks/bashes 24 of them)
  [summary] Athletics detection: melee releases seen 300 (mounted 50) | shots seen 100 (+0 extra projectiles of the same shot ignored) | ranged releases seen by the poll 98 | melee hits by fighters 280 (on foot 240, mounted 40): during a counted release 276, outside one 4 [in action: Other(0) 4]
  [summary] Athletics kicks/bashes charged 8 (23.6 points; by riders 0): kicks 3, shield bashes 5, at their hit with no kick or bash seen 0 | seen starting: kicks 3 (channel 1 0, channel 0 3), shield bashes 5 (channel 1 5, channel 0 0); kick/bash hits 6; free (CostPerKickOrBash 0) 0; each charged once, when it starts; never an attack pause
  [summary] Athletics free (never charged): couched hits within one blow-length of the last 2, attacks while Athletics was off 0, …
  [summary] Athletics exhaustions (empty, f 0): 45 entered, 30 left; the peak zone: left 380 times (a blow took a fighter below his line), re-entered 150 times (by refill)
  [summary] Athletics fighter-time by f (the share of his peak line left): peak (f 1) 71.0%, f 0.5-1 16.0%, f below 0.5 9.0%, empty (f 0) 4.0% of 52000 fighter-seconds
  [summary] Athletics heroes: 2 flagged, 2 party leaders (you, Arcor); lowest a hero reached: Arcor 12.5 of 80
  [summary] Athletics you: skill 90 → pool 90; 25 blows, 1 exhaustion, lowest 0.0 of 90
  [summary] Athletics your formations at the end: 1 Infantry 61 ± 14 (38 men) f avg 0.71, 9 at full strength, health avg 74% | …
  [summary] Athletics health cap: 120 cuts (a wound pulled Athletics down to the health left), biggest 60.0 points, 2400 points in all
  [summary] Athletics regen: 9000 fighter-seconds refilling - at a walk or slower (effort up to 0.40) 7000 s at the walking rate (x1), faster 2000 s at avg x0.71; refills to the top: 38 to full, 12 to a wound's cap; refill curve: near full x0.50 of near empty (RegenRateNearFullPercent 50), x1.39 → x0.69 of a flat refill
  [summary] Athletics refill from empty to the peak line (no blow between): 14 runs, avg 47.3 s (fastest 40.8 s, slowest 61.2 s) - 40.7 s at a walk or slower with these settings, longer while moving faster than a walk
  [summary] Athletics refill effort (speed ÷ current top speed), seconds per tenth (0-0.1 … 0.9-1, above 1): 5200 300 400 900 200 150 150 200 400 900 200, max 1.30
  [summary] speed updates: 900 recomputes asked (…; 0 held a tick by the per-tick budget), the decorator applied attack penalties in 1200 recomputes, run penalties in 1200, horse penalties in 0 (…)
  [summary] run speed check, on foot (÷ the fighter's own top speed when fresh), by f: peak (f 1) engine top x1.00 asked x1.00, moving p90 x0.95 … | … | empty (f 0) engine top x0.70 asked x0.70, … - the engine's top speed follows the curve
  [summary] run speed check, horses (…), by the rider's f: MountMinSpeedMultiplier 1.00 = horses never slow - … - unaffected, as asked
  [summary] walk vs run speeds (tune WalkEffortFraction, now 0.40): on foot walk limit avg 1.80 m/s (n 480), top avg 4.50 m/s (n 480) → walk/top 0.40; horses …
  [summary] Athletics tick cost: avg 0.120 ms, max 1.300 ms per tick over 5400 ticks; fighters polled avg 480, max 1020
  [summary] Athletics errors: none
  ```
  Proves: pools from the skill (min / avg / max, `at the floor` = the troops under skill 50; your pool =
  your skill); `whose skill could not be read` above 0 → tell Claude; the peak zone left and re-entered;
  **run speed `follows the curve`** (`does NOT follow` → tell Claude) and `moving p90` falling row by
  row (the empty row now at x0.70 - step 14); health-cap cuts after a real fight; regen by effort and
  its curve; **`refill from empty to the peak line`** — the avg never below the "at a walk" time (a
  shorter one → the curve is off, tell Claude), close to it when men stood and walked, longer when they
  ran (step 14); **`walk vs run speeds`** — the ratio that
  tunes *Walking pace* (send it along: the setting should sit at, or a little above, walk/top on foot);
  `speed updates` in the hundreds or low thousands for a big battle and `held a tick` 0 or small;
  **`Athletics tick cost` avg well under 1 ms** in a 500+ battle (above 2 ms → tell Claude); detection:
  `outside one` small next to `during a counted release`, `shots seen` ≈ `ranged releases seen by the poll`;
  errors none.

### L5. Attack rate — PAUSE ONLY (step 13)

A tired fighter's attack speed m sets **the pause after each attack**: an attack of D seconds (its
wind-up + release; ranged + the reload after the loose) is followed by D × (1/m − 1) with no new attack
(under 0.1 s: none). **You** (*Your attacks wait out the pause*): the input gate swallows your attack
presses until it ends; held, the attack starts when it does. **The AI** (*Tired AI wait out the pause*):
melee and ranged, on foot and mounted - since step 16 **by input** (*Tired AI keep their guard up (new
way)*: only the attack bits taken out of his own controls, a guard raised when he wants to attack; the
old NoAttack when off), and **the pause survives a step back**. **The animations** play at full speed
(*Slowest attack animation (%)* 100); **the AI's decisions** (*Tired AI also decide to attack less*) are
off by default. Blocking, parrying, kicks, moving and weapon switches are never held.
- `[rate] attached: your attack gate FIRST in the behaviour list (0 of N; … right after MissionMainAgentController at K) - …` (mission start),
  `[rate] mission start: ON - PAUSE ONLY: animations at full speed (AttackAnimationMinPercent 100); after each attack no new attack for D x (1/m - 1) (D = its wind-up + release, ranged + its reload): you (AttackRatePlayerTimer) on - …, AI (AttackRatePaceHold) on - by input (AttackRatePaceByInput on: only the attack bits taken out of his own input, his guard his own, raised when he wants to attack - AiHoldRaiseGuard on), melee and ranged, on foot and mounted; AI decisions (AttackRateAiDecisions) off; never held: blocking, parrying, moving, weapon switches, kicks - read live; …`
  (the old way: `… AI (AttackRatePaceHold) on - NoAttack (AttackRatePaceByInput off: the engine's no-attack flag, step 13's technique), …`)
- Once: `[rate] first slowed fighter this mission: …` (C2), `[rate] first AI timer this mission: …` and
  `[rate] first AI timer ended at … s after … s - time up; …`, `[rate] your attack gate holds for the first time: …`;
  yours: the `[athletics] YOU: first attack pause …`, `… first attack pause ended …`, `… attack pressed …`,
  `… your held attack button started the next attack …` lines (C1, C1b).
- Verbose: `~[rate] AI timer: …`, `~[rate] AI timer ended: …`, `~[rate] AI timer not started: …`.
- Switches mid-battle: `[rate] AttackRatePaceHold switched OFF mid-mission at … s: N held fighters may attack again at once`,
  `[rate] the AI timer is ON again at …`, `[rate] AttackRatePlayerTimer switched OFF / ON …`, `[rate] AttackRateAiDecisions switched ON …`,
  `[rate] AttackAnimationMinPercent now 50 at …`; step 16: `[rate] AttackRatePaceByInput switched OFF mid-mission at … s: new AI timers use NoAttack (the engine's flag); the N running now finish the way they began`,
  `[rate] AiHoldRaiseGuard switched OFF mid-mission at … s: a held AI man who wants to attack only has the attack taken out (from the next frame)`.
- Summary (numbers made up):
  ```
  [summary] attack rate, melee, AI, peak (f 1): animations asked x1.00 - wind-up 0.32 + held 0.08, swing 0.52 (clean, hit nothing 0.60), recoil after a block 0.40, pause 0.45 | cycle 1.40 s (n 900), m 1.00 - the fresh reference
  [summary] attack rate, melee, AI, f 0.5-1: animations asked x1.00 - wind-up 0.32 (x1.00) + …, swing 0.53 (x1.02) …, pause 1.60 (x3.56) | cycle 2.45 s (n 300), m 0.72 → target 1.94 s: 126% - too slow
  [summary] attack rate, melee, AI, f 0.5-1 - timer: 310 (D avg 0.84 s at m 0.72 → asked avg 0.33 s = D x (1/m - 1)); measured: the next attack began avg 1.55 s after the attack's end (n 290), 1.22 s after the timer ended; started before the timer ended: 0 (must be 0)
  [summary] attack rate, melee, AI, empty (f 0): animations asked x1.00 - wind-up 0.32 (x1.00) + …, pause 4.60 (x10.2) | cycle 5.50 s (n 400; with a step back inside 5.80 s n 360, without 2.80 s n 40), m 0.20 → target 7.00 s: 79% - too fast
  [summary] attack rate, melee, AI, empty (f 0) - timer: 450 (D avg 0.84 s at m 0.20 → asked avg 3.36 s = D x (1/m - 1)); measured: … 4.40 s after the attack's end (n 400), 1.04 s after the timer ended; started before the timer ended: 0 (must be 0); the timer's floor D/m avg 4.20 s - the cycle vs it: 131% (n 400), with a step back inside 138%, without 67% (at least ~100% = held as the spec asks)
  [summary] attack rate, melee, AI - verdict: OFF TARGET in 1 of 3 tired bands (…)
  [summary] attack rate, melee, you, empty (f 0): animations asked x1.00 - … | cycle 4.10 s (n 12), m 0.20 → target 4.75 s: 86% - on target
  [summary] attack rate, melee, you, empty (f 0) - timer: 14 (D avg 0.82 s at m 0.20 → asked avg 3.28 s …); measured: … 3.30 s after the attack's end (n 12), 0.02 s after the timer ended; started before the timer ended: 0 (must be 0)
  [summary] attack rate, ranged, AI, f below 0.5 - timer: 30 (D avg 2.60 s at m 0.45 → asked avg 3.18 s …); …; started before the timer ended: 0 (must be 0)
  [summary] attack rate - left out: cycles whose two ends fell in different f bands …; longer than 4 s ÷ m …; readies that ended in no attack …; chained …; with a step back inside (COUNTED since step 16 - the AI timer survives the step back; each band shows them apart) …
  [summary] attack rate - your timer (AttackRatePlayerTimer on at the end): 40 timers (melee 34, ranged 6), avg asked 1.10 s, max 3.30 s; your presses swallowed: 6 during your own attack (no chained blow), 25 during the countdown - the recovery bar flashed 18x; the button held through the end 12x, your attack began avg 0.02 s after (n 12) - near 0 = hold-to-attack works; attacks that started while held anyway: 0 (must be 0 - the input gate missed them); holds begun at your swing's start 30 (ended with no countdown, below 0.1 s: 2); ended early: switched off 1, not you any more 0, mission end 0 (still running at the end, released: 0)
  [summary] attack rate - AI decisions (AttackRateAiDecisions off at the end): scaled in 0 recomputes - … (off by default since step 13: on top of the timer it double-counts)
  [summary] attack rate - AI timer (AttackRatePaceHold on at the end; technique at the end: by input (AttackRatePaceByInput on: …); after each attack of a tired AI fighter, melee and ranged, on foot and mounted): 900 holds (by input 900, by NoAttack 0; melee 800, ranged 100, mounted 60), avg 0.95 s, max 3.40 s at avg m 0.55; by f: …
  [summary] attack rate - AI timer, not held: at full strength 2000, not needed (below 0.1 s) 150, the next attack already readied at the attack's end 60, the attack's length not measured 5, covered by a scripted step back (a NoAttack hold waiting for the step to end whose time ran out first) 0 | not started by the tick: …
  [summary] attack rate - AI timer ends: time up 880, an attack started anyway 2 (must be about 0 - the hold stops attacks), … | lifted by us 880, … | the next ready came avg 0.20 s after a hold ended (n 700) - the AI's own re-decision after the hold lifts (NoAttack cost 1-3 s; by input he readies at once if he still wants to)
  [summary] attack rate - guard by f (…): peak (f 1) 45% (n 900) | f 0.5-1 47% (n 400) | f below 0.5 46% (n 150) | empty (f 0) 44% (n 60) | while held by the AI timer 52% (n 90)
  ```
  Proves, in this order:
  1. **No slow-mo**: every row `animations asked x1.00` and its wind-up / swing / draw / reload about
     `(x1.00)` of the peak's (a hit swing may run a little longer - `clean` is the pure one).
  2. **The timer holds**: every `… - timer:` row `started before the timer ended: 0 (must be 0)`; `your
     timer` → `attacks that started while held anyway: 0`; `AI timer ends` → `an attack started anyway`
     about 0. Above 0 → the engine ignores NoAttack (AI) or the input gate's order is off (you) - tell Claude.
  3. **Its size**: `asked avg` = D avg × (1/m avg − 1) in every row; `measured … after the attack's end` ≥
     asked; `after the timer ended` = your fingers (near 0 when you hammer) or the AI's own re-decision.
  4. **Hold-to-attack**: `your attack began avg 0.0x s after` near 0.
  5. **The rate** (the `verdict`): target = the peak's cycle ÷ m. Your rows near target when you
     attacked as fast as you could. The AI's rows: its own gap after an attack does not shrink, so
     mild bands can read `too slow` (the NoAttack re-decision) and empty ones `too fast` (its own idle
     gap runs inside the timer) - that is the D-based timer as specified; C7 tells which feels right.
     Step 16: each timer row's `the timer's floor D/m … the cycle vs it: P%` must be at least ~100% -
     THE check that the pause as specified held (before step 16 the empty band read ~50%: a step back
     dropped the pause); cycles with a step back inside now count (shown apart in the band line).
  6. **Blocking untouched**: the `guard by f` rows within ~10 points of the peak row, `while held` not below it
     (step 16's sharper version: L6b's GUARD line).

### L6. Step back

- `[stepback] mission start: ON - …`; first tick `[stepback] this mission allows step backs while it is in battle mode (now: …)`
  — or `… is a tournament or arena fight (…) - no step backs here (vanilla AI)` / `… is a naval battle (moving decks) …`.
- The first one in full and its end (D1 - step 16: every 0.25 s of it written into the end line, and the
  input frames); verbose every start, end and refusal; switches (D3, B3; step 16:
  `[stepback] StepBackBackpedal switched OFF mid-mission at … s: new step backs are scripted walk (…); the N running now finish the way they began`).
- Summary — 8 lines:
  ```
  step back - technique: a backpedal - a backwards movement written into the AI's own input (…) …; settings at the end: ON - … (the old way: "a scripted step - …"; both: "MIXED this battle (StepBackBackpedal switched): …")
  step back rolls after AI melee swings on foot, by f (the chance must be 0% at full strength and rise as f falls): peak (f 1) 900 swings, chance avg 0%, dice yes 0 (0%) | f 0.5-1 700 swings, chance avg 27%, dice yes 190 (27%) | f below 0.5 … | empty (f 0) 150 swings, chance avg 100%, dice yes 150 (100%); not rolled: you 40, riders 30, not a field battle (…) 0
  step back starts: dice yes 620 → started 480 (holding a line 200, charging 270, no formation 10), most at once 35; not started 140 (…reasons…)
  step back ends: 480 - completed (time up) 130, arrived (StepBackDistance covered) 300, cut short 50 (left the field 30, formation order changed 15, the ground ends behind him (edge ahead) 2, …)
  step back moves: avg 1.80 m of 2.00 asked (min …, max …, reached the spot … of 480), lasted avg 1.20 s (n 480) - about 1.50 m/s
  step back facing (THE risk: a turned back) - at the start: facing his enemy … | mid-step (… sampled): facing his enemy …, side-on …, back turned …; moving away …, … | at the end (…): … | every 0.25 s (N samples): facing his enemy …, side-on …, back turned …; step backs with the back turned at ANY sample K of M (P%) (must be about 0)
  step back guard: hits taken while stepping back N - blocked B (P%), landed … | everyone else on foot: … hits, blocked … (Q%) | swings started while stepping back 0 (0 expected: StepBackHoldAttacks is on)
  step back release check: N released through the engine - scripted movement still on right after 0 (must be 0), our flags (NoAttack, DoNotRun) cleared by hand 0; backpedals ended by stopping the input M (nothing of ours left in the engine) | at mission end: K were mid-step (released then), overdue (past their time) 0 (must be 0), scripted movement still on after that release 0 (must be 0)
  ```
  Proves: the `peak (f 1)` row ALWAYS `chance avg 0%, dice yes 0`, the rows below rising (≈ 100% × (1 − f));
  **`facing`: `back turned` near 0 - mid-step, at the end AND at ANY 0.25 s sample (P% under ~5%)**,
  `facing his enemy` the big number; **every `(must be 0)` is 0**; `moves`: the backpedal should get most of the
  2 m (`arrived` a big share; the old walk managed ~0.6 m); `guard` `P%` not well below `Q%`; `not started …
  engine did not take the scripted position N` large → the engine refuses the call (old way only).

### L6b. AI holds — the guard really up and the backpedal (step 16)

The per-man input component behind both new ways, and the timer surviving a step back. The summary
header names the technique: `==== scene …, mod ON, AI holds: Input (the AI's own input: guard up, backpedal) ====`
(or `AI holds: Legacy (NoAttack + the scripted walk)`, `AI holds: the timer Input, the step back Legacy`,
`AI holds: mixed (a switch changed mid-battle)`).
- Summary — 4 lines (numbers made up):
  ```
  AI holds - technique: the AI timer by input (AttackRatePaceByInput on: …) - this battle 1500 holds by input, 0 by NoAttack; the step back backpedal (a backwards input, facing his enemy) - this battle 900 backpedals, 0 scripted walks
  AI holds - GUARD (melee hits on AI fighters on foot that were blocked or parried; THE fix target: held and stepping back close to everyone else): held by the timer 31% (n 400), stepping back 28% (n 200) (of them also held by the timer 30% (n 150)), everyone else 35% (n 1200) - of them tired (below the peak line) 30% (n 700), at full strength 42% (n 500) - gap to everyone else: held -4 points, stepping back -7 points
  AI holds - the input hook (AgentComponent.OnAIInputSet): 380 men hooked (a component each, added the first time he was held), the callback already on for 380 of them (another mod's component - RTS Camera Command System turns it on for every agent), turned on by us for 0, turned off again when idle 0 times; calls while held 90000 (about 30.0 a second per held man); the attack bits taken out in 12000 calls (a guard raised in 9000, his own guard kept in 2900, a ready cancelled in 100), a backpedal written in 25000 calls; the player or a non-AI agent passed untouched 0; holds / backpedals the engine never called us during 0 / 0 (must be 0 - else the hook is dead: switch the new ways off and tell Claude); errors 0
  AI holds - the timer survives a step back: holds that overlapped a step back 850 (both ran at once, his attacks held until the later of the two ends); NoAttack holds deferred behind a scripted step back 0 (set when the step ended 0, covered by the step back 0) | AI attacks that started while a hold or a step back held him anyway 3 (must be about 0)
  ```
  Proves: **the GUARD line's gaps small (≤ ~10 points)** - the playtest before step 16 read held 2%,
  stepping back 5%, everyone else 33%; the hook alive (`calls while held` well above 0, `never called us
  during 0 / 0`, `errors 0`); `attack bits taken out` > 0 (the AI still wanted to attack while held - and
  did not); `AI attacks that started while … held him anyway` about 0; `holds that overlapped a step back`
  > 0 in any battle with step backs (the timer survived them). With RTS Camera the callback is `already on`
  for everyone; without it we turn it on and off per man (`turned on / off` counts).
  Reading `never called us during` (step 17, REVIEW R28): judge it by the RATIO - a handful against hundreds
  of holds by input, with `calls while held` at tens a second per held man, is not a dead hook (a man knocked
  down through a short hold may not be asked for input); most holds never called, with calls near 0, is.
- Once per battle: the first held man and the first step back in full (D1's lines) - the input bits
  before → after, the vector written, the calls; `melee hits taken while held H (blocked B)`.
- **Stop and tell Claude**: `[rate] WARNING: … the engine never called our input hook …` together with
  the summary's `never called` count being a large share of the holds (the warning alone fires on the first
  one - see the ratio rule above), or `[error]` lines at the site `hold.input-hook` (the component's own
  swallowed errors).
  Must NOT happen (the `not rolled` / `not started` reasons): you, riders, archers shooting; *Shield
  wall*, *Square*, *Circle*; retreating or routing; ladders, siege towers and engines (`busy (the game's
  own check: …)`); a wall's edge or stairs (`spot not level`, `no straight way back`, `spot off the navmesh`);
  tournaments, arena, naval (`not a field battle (…)`).

### L7. The HUD — your bar, the recovery bar, the orders strip and the ALT markers

- Attach, first tick: `[hud] attached: player bar (…)`, `[hud] attached: recovery bar (AttackRecoveryView, movie TraxAttackRecoveryBar, prefab installed) - …`
  and `[hud] attached: orders strip (OrderStripView, movie TraxOrderStrip, prefab installed) - shown while ModEnabled, AthleticsEnabled and ShowInOrderMenu are on, … and while the orders menu is open`.
  (Each view's build / removal lines have their own rate bucket since step 13.)
- The recovery bar (step 13): `layer created …`, `first values pushed at … - full (no pause running); bar 205 x 14 px, … 54 px from the bottom (your Athletics bar's row 30 + 24; …)`;
  the first pause in full (`first pause shown at … - empty at your attack, now refilling over … s (D … s at attack speed x…), "1.2 s" inside it, counting down`),
  the first flash (`first flash at … - you pressed attack with … s of your pause left (2 pulses of 0.12 s)`);
  `layer removed at … - ShowAttackRecoveryBar off` / `- your Athletics bar (ShowPlayerBar) or your pause (AttackRatePlayerTimer) is off` and the player bar's reasons.
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
- The ALT markers (step 20, E4): `[hud] attached: ALT markers (AltMarkerView, movie TraxAltMarkers, prefab installed) - … while the game shows its formation markers (ALT held or the orders menu open - read live from its own marker layer)`;
  `layer created …` once (later ALT presses and releases go to the verbose log only); **`first shown at … (show #1) - technique: …; screen W x H px, UI scale S; N markers (yours a, allies b, enemy c): <each marker: team, formation, men, its point, its widget's size, the distance>; labels (<layout>, centre x / top y): <each label>; no label: <formation> - behind the camera | faded (closer than 5 m or fading) | not laid out yet | no tracked men | enemy (AltMarkersShowEnemy off)`**
  (the proof: a label's centre x = its marker's x; its top y = the marker's y + half its height + the gap × the UI scale);
  `values at … (show #1): yours 1 Infantry 72% ± 8 HP 81% (40 men, f 0.93) | … - ± is 1.00 std, health on, enemy on` once per battle (verbose at each later show).
  The FALLBACKS, each with its why (the first of each kind in full, later ones verbose), counted in the summary:
  `[hud] ALT markers: a marker without its widget at … (show #N) - 1 of 6 markers had no widget at their point (layer MissionFormationMarker, movie FormationMarker: 6 markers, 5 marker widgets): … - a nominal marker size for those`;
  `[hud] ALT markers: FALLBACK at … (show #N) - layer MissionFormationMarker, movie FormationMarker: 6 markers, 0 marker widgets - the game's points with a nominal marker size (60 x 108 UI px) until the markers go`;
  `[hud] ALT markers: FALLBACK at … (show #N) - no MissionFormationMarker layer on the screen - our own projection of the same point (the formation's median + 3 m), the game's rule copied (ALT held or the orders menu open), until the markers go; the next show tries the game's markers again`
  (or `- the MissionFormationMarker layer holds no FormationMarker movie over the game's marker ViewModel - …`: another mod replaced the markers).
  At the battle's end, only if some fighters were on a team the averages do not cover: `[athletics] formation averages: N fighters are on a team past index 7 - … tell Claude`.
- Summary:
  ```
  [summary] hud: player bar (movie TraxPlayerAthleticsBar) - on screen 312.4 s of 340.2 s (92%); layer built 2x, removed 2x (ShowPlayerBar off 1, mission end 1); hidden: ShowPlayerBar off 7.7 s, not a fight 20.1 s; 3120 refreshes; errors 0
  [summary] hud: player bar colours on screen - green 180.0 s (58%), blue 60.0 s (19%), yellow 40.0 s (13%), orange 20.0 s (6%), red 12.4 s (4%); 14 colour changes; exhausted shown 2x (8.2 s); wounded part shown 45.0 s (lowest usable 62% - the last 38% of the bar dark)
  [summary] hud: recovery bar (movie TraxAttackRecoveryBar) - on screen 312.4 s of 340.2 s (92%); layer built 2x, removed 2x (…); hidden: …; 3120 refreshes; errors 0
  [summary] hud: recovery bar - pauses shown 40 (avg 1.10 s, longest 3.30 s), 60.2 s on screen not full; flashes shown 18 (FlashBarOnEarlyAttack on at the end) - your pauses and swallowed presses are in the attack rate lines
  [summary] hud: orders strip (movie TraxOrderStrip) - on screen 48.1 s of 340.2 s (14%); layer built 12x, removed 12x (orders menu closed 11, mission end 1); hidden: not a fight 20.1 s, orders menu closed 272.0 s; 480 refreshes; errors 0
  [summary] hud: orders strip - opened 12x: under the cards in 12, the compact panel in 0; technique: the live vanilla cards (layer MissionOrder: 16 cards); card layouts seen: 16 cards in 2 sets, set 1 drawn; cells placed 36 (lifted to the screen's edge 0), card changes 0, short mismatches 0 (under 1.0 s), card re-scans 0, values pushed 40; fallbacks: none
  [summary] hud: ALT markers (movie TraxAltMarkers) - on screen 32.5 s of 340.2 s (10%); layer built 14x, removed 14x (markers hidden 13, mission end 1); hidden: not a fight 20.1 s, markers hidden 287.6 s; 325 refreshes; errors 0
  [summary] hud: ALT markers - shown 14x, on screen 32.5 s; technique: the game's own formation markers read live (their points and their widgets' sizes) (layer MissionFormationMarker, movie FormationMarker: 6 markers, 6 marker widgets); formations labelled: yours 3, allies 0, enemy 3 (most at once 6), frames with a label pinned at the screen's edge 0, values pushed 90; fallbacks: none; errors 0
  ```
  The ALT line proves: *shown* = how often the game's markers came up (ALT presses + orders-menu opens) while
  the numbers were on; *technique … read live* with no `(shows: …)` mix and *fallbacks: none*; *formations
  labelled* names both sides (enemy > 0 with *Enemy formations too* on); *errors 0*.
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

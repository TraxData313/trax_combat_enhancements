# Playtest — one session, start to finish

Anton plays this ONCE, after the build is finished, and hands Claude the log. Seven parts in
order, about **3 hours 15** (A 10 min, B 15, C 25, D 60, E 20, F 55, G 10; the optional bits add ~20). **Short on
time after step 16? D4 alone (the A/B of the new guard and backpedal, 20 min) answers the newest
questions; after step 21, D5 + D6 (the shield wall and the archers, 25 min); after step 23, D7 (lines brace by
their orders, 15 min - since step 24 with the "ready 34/50" count on ALT and in the orders menu); after step 28, D8
(a tournament: the opponent fights on to ~20%, 10 min; after step 29 its step 4 - block your way back from empty; after step 30 its step 5 - the tired opponent guesses his blocks); after step 32, D9 (the swing stack: save swings up and burst, the kick refused at 0, 10 min); after step 33, D10 (the sprint: Left Ctrl, a charge order, 15 min; step 35 - a tap, the tired floor); after step 41, D11 (a shield wall that holds its slots, ON then OFF, 10 min); after step 42, D12 (the small step out, L 1 / 0 / 2 live, 10 min). The impact system (steps 47-54 - staggers, shields,
knockdowns, riders, the charge, the AI punishing a window): **THE IMPACT MORNING SCRIPT** in part D, just before D16 - six battles in order, ~90 min.** Each part says what to do, what you
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
`[stepback]` `[brace]` `[sprint]` `[wall]` `[fever]` `[hud]` `[error]` `[log]` — search one to follow an area. Every battle ends with a
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
- You see: a normal start; at the main menu *"Trax Combat Enhancements 1.0.0… loaded - settings in
  config.json."* No error dialog.
- Log:
  - `[load] ==================== Trax Combat Enhancements 1.0.0+… ====================` (after the `+`:
    the git commit it was built from), `[load] dll: …\Modules\TraxCombatEnhancements.Dev\bin\Win64_Shipping_Client\TraxCombatEnhancements.dll (built …)`
    (the dev copy runs), `[load] module: TraxCombatEnhancements.Dev (the dev install - tools\deploy.ps1)`,
    `[load] game: v1.4.8.…`, `[load] modules (N): …`
  - `[compat] Realistic Battle Mod (RBM) not enabled - good`
  - `[config] defaults: defaults.json (embedded in TraxCombat.Core.dll), 86 keys for 86 settings - every default read from it`
  - `[config] first run: created config.json with every default and a plain-words explanation beside each value`
  - `[config] settings in effect (70, version 0):` and 70 lines like `[config]   DamageRandomPercent = 50`
  - **Step 22 - YOUR config.json today** (2026-09-29, read only) is format 4 and holds exactly the two old defaults step 22
    changes, so your next start migrates them, once:
    `[config] migrated config.json at startup: RegenRateNearFullPercent: 50 → 100 (format 4 → 5, the old default; step 22, Anton for slower battles: the refill is a straight line again - empty to full in the refill time at a walk, no faster when low)`,
    `[config] migrated config.json at startup: MinMoveSpeedMultiplier: 0.6 → 0.3 (format 4 → 5, the old default; step 22, Anton for slower battles: an empty man runs at 30% of his pace again)`
    and `rewrote config.json as format 5 with the 2 migrated value(s)` - the settings dump then reads 100 and 0.3, and the three
    new block costs come in at their defaults (`CostPerShieldBlock = 1`, `CostPerWrongSideShieldBlock = 5`, `CostPerWeaponParry = 2`).
    The next start reads format 5 and says nothing. (A value you set yourself - say a run floor of 0.5 - stays.)
  - **Step 20b (history)** - your config.json of 2026-09-28 (2026-09-28 09:46) is format 3 and holds YOUR playtest values
    `MinMoveSpeedMultiplier` **0.5** and `AttackAnimationMinPercent` **90** - not the old defaults, so the migration
    rightly leaves them alone (nothing is logged for them). **To play the new defaults** (run 0.6, swing 85): MCM →
    Defaults → "Revert all to defaults", or set the two by hand. Note: 90 now means the LINE (x0.90 at empty, x0.95
    halfway), not step 13's max(m, 0.90).
  - (A format-3 file still at the OLD defaults instead gets, once:
    `[config] migrated config.json at startup: MinMoveSpeedMultiplier: 0.7 → 0.6 (format 3 → 4, the old default; step 20b, Anton after his playtest: an empty man runs at 60% of his pace - the sweet spot)`,
    `[config] migrated config.json at startup: AttackAnimationMinPercent: 100 → 85 (format 3 → 4, the old default; step 20b, Anton after his playtest: a tired man's attack animations slow in a straight line to 85% at empty - the pause in seconds stays as it was)`
    and `rewrote config.json as format 4 with the 2 migrated value(s)` (the settings dump then reads 0.6 and 85); a value you
    set yourself stays; the next start reads format 4 and says nothing.
    A format-2 file (step 13) gets `MinMoveSpeedMultiplier: 0.3 → 0.6 (format 2 → 4, …; step 14: …)` and the 100 → 85 above;
    a format-1 file (before step 13) also `AttackRateAiDecisions: true → false (format 1 → 4, …)` and
    `PlayerBarOffsetBottom: 54 → 30 (…)`.)
  - `[mcm] MCM (Mod Configuration Menu) is not loaded - no settings page; the mod runs on config.json alone. That is fine.`
    — or, if another enabled mod carries MCM's DLL in its own folder (step 12, your 21:08 session):
    `[mcm] MCM's module is not enabled - no settings page; config.json only. (An MCM 5.… DLL is loaded - another mod carries it - …)`
    — one line, and no `[mcm]` line after it.
- Broken: the game will not start or names the mod in an error; no `[load]` lines; any
  `defaults.json PROBLEM:` line; `[mcm] … not ready yet` with MCM's module off.

**A2. The config file — and VerboseLogging on, by hand.** Alt-Tab, open `config.json`.
- You see: a header (where the file lives, that edits apply at the next battle start, three ways back
  to the defaults), then 11 sections — *Master switch, Damage randomness, Athletics, Tired fighters,
  Tired fighters step back, Battle pace (AI), Refill, Your Athletics bar, Orders menu strip, Formation
  markers (hold ALT), Advanced* — and above
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
- Log (step 59 - two pages): `[mcm] settings pages registered at main menu (attempt 1): MCM 5.…, page "Trax Combat Enhancements" (20 settings in 7 groups; group "Defaults": "Revert all to defaults") and page "Trax Combat Enhancements - Advanced" (255 settings in 16 groups; group "Defaults": "Revert all to defaults" and "Save current values as a defaults file") - 275 settings in all, format "none" (config.json is the only store), Default presets = the mod's defaults (defaults.json).`
  (`at retry (attempt 2)` is fine — MCM woke a moment after us; the first try is now always at the
  main menu, where MCM builds its services — step 12); the settings dump shows
  `VerboseLogging = true (default false)`.
- Broken: `[mcm] MCM … never became ready - gave up after 31 attempts …` with MCM enabled (the page
  is missing — send the `[mcm]` lines); a `not ready yet` line with no `registered` or `gave up` after it.

**A5. The MCM pages (step 59).** Options → Mod Options → *Trax Combat Enhancements*: the SHORT page - *Master switch*, *How
hard hits land*, *Tiredness (Athletics)*, *How the AI fights*, *Staggers and knockdowns*, *Sprinting*, *On screen*, then *Defaults*
with one button (*Revert all to defaults*) - 20 settings you can read at a glance, plain labels, nothing about seams or levers.
Then *Trax Combat Enhancements - Advanced* right under it in the list: every other setting, by system (Damage first, Advanced last,
the two Defaults buttons just above it). Revert on either page reverts BOTH pages (the other page shows the defaults when you open it).
- Broken: a setting on both pages or on neither (compare with config.json - main settings first, then the `ADVANCED - for tuning` banner);
  the short page longer than one screen.
- (Before step 59, one page:) the same 11 groups — *Master switch* (Mod enabled) on top, *Battle pace (AI)* (step 21) after
  *Tired fighters step back*, *Hold ALT: formation markers, Athletics and fever bars* (step 20; steps 44 and 46 renamed it) just before *Advanced* at the bottom, and
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
run at **60%** of his pace (step 20b, your "sweet spot"; 0.7 in step 14, 0.3 before). **PAUSE ONLY (step 13)**: no slow-mo -
every swing, thrust, draw, throw and reload plays at nearly full speed (**step 20b: a straight line down to 85% at
empty** - a tired man's swing looks a little heavier, x0.925 halfway, full speed at the peak line); what slows is the
**pause after each attack** — an attack of length D at attack speed m is followed by D × (1/m − 1) in which no new
attack can start (at empty: four attack-lengths). **The pause in seconds is exactly as before**: D is measured at full
animation speed, so the slower swing only adds its own moment (at empty ~0.14 s on a 0.8 s attack). For
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
  inside it: `0.1 s`, `0.4 s`, … up to `≈ 3 s` at empty - the SAME seconds as before step 20b); **every swing
  itself plays a little slower as you tire — visibly heavier near empty (85%), never slow motion** (step 20b).
  At ~16 the word reads *Exhausted* in red. Run: about **30%** of your
  pace — a trudge (step 22, Anton: "to 30% again"; 60% until then). Block: normal (but it costs a little -
  C1d). Stop: about 3 s later the bar climbs — **in a straight line** (step 22: the even refill again): 30 s to
  half, 45 s to green, 60 s to full.
- Then empty yourself again and RUN flat out while it refills: about twice as long (120 s to full).
- Tell Claude: is 30% right for the run when empty (*Tired fighters* → *Run speed when empty (x)*)? Is 85% the
  right heaviness for a tired swing (*Tired fighters* → *Attack animation speed when empty (%)*: 100 = full speed
  to compare)? The even refill vs step 14's curve: *Refill* → *Refill speed near full (%)* 50 = the curve to compare.
- Log (always):
  - `[rate] attached: your attack gate FIRST in the behaviour list (0 of N; behaviours pre-tick from the end, so it runs right after MissionMainAgentController at K) - …`
    (at the mission start - `could not attach your attack gate` → tell Claude) and, at your first pause,
    `[rate] your attack gate holds for the first time: it pre-ticks after MissionMainAgentController (gate 0, controller K of N …)` — **`WARNING: your attack gate's order is off` → tell Claude**
  - `[athletics] YOU: first attack pause this battle at … s - your melee attack (wind-up 0.28 + swing 0.55 = D 0.83 s at full animation speed; played 0.84 s - the slower swing, not in the pause) ended at attack speed x0.93 (f 0.91) → no new attack for 0.06 s = D x (1/m - 1), until … s; the hold began at your release's start (expected … s) …`
    — the pause must be D x (1/m − 1) with D at FULL animation speed (step 20b: "played" is the longer time on the
    clock; it is shown only when it differs) (pauses under 0.1 s are skipped: the first ones may come a few
    swings below the line); later ones rate-limited: `[athletics] YOU: attack pause 1.20 s at … s (D 0.80 s at x0.40, melee) until … s`,
    `[athletics] YOU: attack pause ended at … s`
  - `[athletics] YOU: Athletics skill 90 → pool 90 (the skill x1.00, at least 50); full strength down to 68 (75%); a blow costs you 5.6 - about 5 blows at full strength, 16 to empty`
    — **the skill must match your character screen**
  - `[athletics] YOU dropped below full strength at … s: 61.9 of 90 (the line is 68) after 5 blows this mission - f 0.92: attacks x0.93, run x0.94, damage upside 92% of the full`
  - `[athletics] YOU are exhausted at … s: 0 of 90 after 16 blows this mission - attacks at 20% speed, run x0.30, no damage upside until you rest (refill starts 3.0 s after your last blow, kick, bash or WRONG-side block - a clean block or parry lets it run)` (step 29; with OnlyWrongSideBlocksStopRefill off: `… after your last blow or paid block)`)
  - `[athletics] YOU are off empty at …`,
    `[athletics] YOU are back at full strength at … s: 67.5 of 90 (the line is 68) - up from empty in 45.0 s of refill (45.0 s at a walk or slower; the same rate all the way (RegenRateNearFullPercent 100))`
    — standing or walking the "up from empty" time ≈ 45 s (step 22: the even refill; step 14's curve took 40.7 s); running, longer,
    `[athletics] YOU are back to full at … s: 0 → 90 of 90 in 60.0 s of refill (at a walk or slower 60.0 s, faster 0.0 s; avg rate x1.00; empty to full takes 60 s at rest, 120 s at a full run; the same rate all the way (RegenRateNearFullPercent 100))`
    — after the run `… avg rate x0.5…`; **60.0 s at a walk**, the bar rising by the same amount every second (step 22)
  - the colours: `[hud] player bar: BLUE for the first time this battle at … - f 0.97, …`, then
    `YELLOW`, `ORANGE`, `RED`, `[hud] player bar: EXHAUSTED shown at …`
  - the recovery bar: `[hud] recovery bar: first pause shown at … s - empty at your attack, now refilling over 0.40 s (D 0.82 s at attack speed x0.67), "0.4 s" inside it, counting down`
- Broken: your pool is not your Athletics skill; **a swing in slow motion** (step 20b: only a little heavier -
  the summary's `animations asked` x1.00 at the peak, ~x0.96 f 0.5-1, ~x0.89 below 0.5, x0.85 empty); a pause
  that got LONGER than before step 20b (the timer rows' `D avg` must stay ~0.8 s melee - the played time sits
  beside it); no pause at empty; a swing that costs nothing;
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

**C1d. Blocking costs Athletics too (step 22).** At full strength, face one enemy (a sword and shield is
easiest) and let him swing at you:
1. **Block with the shield, the right way** (guard towards his swing) 5 times. You see: the number drops **~0.56
   each** — you are a hero AND the party leader (1 × 0.75 × 0.75); a soldier pays 1, a companion 0.75. The
   recovery bar stays **full and quiet**: a block is never an attack pause.
2. **Block with the shield the WRONG way** (guard to the other side - the game's own "wrong side" block) a
   few times: **~2.8 each** (5 × 0.56) — clearly more.
3. **Parry with a weapon** (a two-hander or a sword without a shield): **~1.1 each** (2 × 0.56).
4. Keep blocking a busy enemy for 20 s **the right way** (or parrying): since step 29 **the bar keeps refilling
   while you block** — each clean block takes its ~0.56 / ~1.1, the refill runs on underneath and you net UP
   (a 90 bar refills 2.25 per 1.5 s at a stand). Now block the WRONG way for a while: **the refill stops** —
   a wrong-side block restarts the 3 s delay, like a swing. (`OnlyWrongSideBlocksStopRefill` off = step 22:
   every paid block stops it.) Step back out of reach: the refill starts ~3 s after your last swing or wrong block.
5. Arrows into your shield: **free** (for now) — the number does not move.
6. Watch the front: shield-wall men who block a lot tire too, a little — their bars in the orders strip /
   under ALT drop even when they barely swing.
7. *Athletics* → *Cost per shield block (points)* 0: a right-side block is free at once.
- Log (always; the first of each KIND per battle, in full):
  `[athletics] YOU: shield block (right side) at … s cost 0.56 Athletics (1.00 x0.56 hero party leader): 90.0 → 89.4 of 90 (f 1.00 → 1.00) - the first of this kind this battle: a melee blow you block costs YOU CostPerShieldBlock x your hero / party-leader multipliers, once per blow, and the refill keeps running (only a WRONG-side block restarts its delay - OnlyWrongSideBlocksStopRefill); blocking is never held or slowed`,
  the same for `weapon parry` (1.13, CostPerWeaponParry), and for `shield block (WRONG side)` (2.81, CostPerWrongSideShieldBlock)
  ending `… and restarts the refill delay (a WRONG-side block - a clean one would let the refill run); …`;
  later ones only with `VerboseLogging`: `~[athletics] shield block (right side) (on foot): <name> (you) - cost 0.6 (x0.56: hero party leader), … - the refill runs on`
  (the AI's the same, bucket `athletics-block`; a wrong-side one without the tail).
- Summary (L4): `Athletics blocks paid by the defender N (P points; by riders R): shield right side a (…), shield WRONG side b (…), weapon parries c (…) | by you y (…), AI heroes h (…), other AI o (…) | free (cost 0) 0, while Athletics was off 0, the same blow seen again (not charged twice) d, missiles stopped by a shield (free) m; …`
  — **tell Claude the three counts and the points against the blows line's total** (how much of the drain blocks
  are); `the same blow seen again` is the dedupe at work (a swing touching a guard twice) - fine at any size.
  Step 29 adds at its end: `| the refill delay restarted by W (right side 0, WRONG side W, parries 0), the refill kept
  running under K (right side a, WRONG side 0, parries c) - OnlyWrongSideBlocksStopRefill (step 29)` — **only the WRONG
  side restarts with the switch on**; a right-side or parry count under "restarted" means the switch was off.
- Broken: a block costing a whole blow (5.6 for you) or nothing; a wrong-side block costing the same as a right-side
  one every time (the side is not read); the recovery bar emptying on a block; arrows into your shield costing.

**C2. Recruits run dry, legionaries keep going.** Let the lines meet; watch the front from close by.
- You see: recruits' swings stay quick, but after 2 swings they wait longer and longer between them
  and after ~5 they strike about once in four swing-lengths — between blows they stand, guard up,
  still blocking; legionaries keep their pace for ~4 swings and last ~13; exhausted men lag when a
  formation moves and fresh men overtake them — an empty man trudges at 30% of his pace (step 22). **No slow-motion swings anywhere** - tired men's swings only
  look a little heavier (step 20b: x0.85 at empty).
- Log:
  - verbose samples: `~[athletics] pool at spawn: Imperial Recruit - Athletics skill 20 → pool 50 (the floor); 2 blows at full strength, 5 to empty`,
    `~[athletics] blow melee (on foot): … - cost 10.0, 40.0 → 30.0 of 50 (f 1.00 → 0.80) - below full strength`
  - once: `[speed] first exhaustion this mission: <name> at … - properties before: … → after UpdateAgentProperties: … - the penalties are in the agent's properties`
    (the three animation values move a little with the run value - step 20b: `x0.9… / x0.9… / x0.9…, run x0.…`)
  - once: `[rate] first slowed fighter this mission: <name> at … - attack speed x0.97 (f 0.96); animations (AttackAnimationMinPercent 85 → x0.99): swing … → … (each x0.99 as asked); AI decisions (AttackRateAiDecisions off): … (unchanged, as asked); the timer (you: AttackRatePlayerTimer on, AI: AttackRatePaceHold on) comes after each attack …`
    — **`NOT … as asked - tell Claude`** (step 20b: the animation factor = 0.85 + 0.15 × his f)
  - once: `[rate] first AI timer this mission: <name> at … s - his melee attack (D 0.80 s: wind-up + swing at full animation speed; played 0.82 s - the slower swing) ended at … s at attack speed x0.85 (f 0.81) → no new attack for 0.14 s = D x (1/m - 1), until … s; …`
    — the pause from the FULL-SPEED D (step 20b); with NoAttack (the old way) `scripted flags 0 → 2 (NoAttack set: the engine took it)`
    — **`NoAttack NOT set` → tell Claude**; and `[rate] first AI timer ended at … s after … s - time up; …`
- The summary (appendix L4, L5) settles whether the engine honours it: the `attack rate, melee, AI,
  <band>` rows read `animations asked` x1.00 at the peak down to x0.85 at empty (step 20b) and their wind-up /
  swing about `(x1/animation)` of the peak's - x1.18 at empty (the slower swing, as asked); the timer rows'
  `D avg` stays at the full-speed ~0.8 s with `played …` beside it; the
  `… - timer:` rows `started before the timer ended: 0`; `attack rate - AI timer ends: … an attack
  started anyway 0`; `run speed check, on foot … - the engine's top speed follows the curve`;
  `Athletics tick cost: avg … ms` (well under 1 ms).
- Feel: tired men swing a little heavier (step 20b) and strike less often — longer gaps, guard up in
  between — while fresh men keep their pace. Say if it is too much or too little.

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
  `[speed] speed settings now: when empty attacks at 50%, run x0.60, horses x1.00; full strength at 75% of the pool - N fighters get new speeds …`. Back to 20.
- *Refill* → *Refill speed near full (%)* 50 → 100 while your bar refills (step 14): from then on it
  climbs at one even speed (the old refill). Log `[config] RegenRateNearFullPercent: 50 → 100 (source: MCM)`;
  your next `YOU are back to full …` line ends `the same rate all the way (RegenRateNearFullPercent 100)`. Back to 50.
- *Tired fighters* → *Your attacks wait out the pause* off (while your bar refills): the recovery bar
  goes, you attack freely at once. Log `[rate] AttackRatePlayerTimer switched OFF mid-mission at … s: your attacks are no longer held …`
  and `[athletics] YOU: your attack pause released at … s - switched off (…): attack at once`. Back on.
- *Tired fighters* → *Attack animation speed when empty (%)* 85 → 50 (step 20b): tired swings get clearly heavier
  (x0.75 halfway, x0.50 empty), the pause's seconds stay the same - log `[rate] AttackAnimationMinPercent now 50 at … s: N tired fighters get their attack animations x (0.50 + 0.50 f): full speed at the peak line, x0.75 halfway, x0.50 empty over the next ticks (…) - a slower swing on top of the timer; the pause in seconds unchanged`.
  Then 100: full speed (`… at full speed over the next ticks (…) - the timer alone slows them`). Back to 85.
- *Your Athletics bar* → *Attack recovery bar* off / on: it goes and comes back (`[hud] recovery bar: layer removed at … - ShowAttackRecoveryBar off`).
- *Damage* → *Spread (± %)* 50 → 0: the same blow deals the same number. Log per hit
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

**C8. Every hit 25% weaker (step 25, +5 min).** *Damage* → *Damage reduction (%)* is 25: every hit on a person,
a horse or a shield lands a quarter lighter, AFTER the ±50% roll — an average 100 blow lands 75 (anything from
38 to 113). Falls and gates keep the game's damage.
- In the C setup, hit the same looter with the same weapon ten times (*Report Damage* on): the "Delivered N
  damage" numbers sit about a quarter under what they were (a pitchfork's 18-ish average becomes ~14).
  Men take visibly more blows to drop.
- Log, at the battle's start: `[damage] mission start: … - read live on every hit; overall damage reduction (DamageReductionPercent) 25% after the roll, rolled or not (an average 100 hit lands 75; falls and objects never)`;
  the first blow anyone lands: `[damage] first reduced hit this mission - melee on a person: … → …, <weapon>: the game's 18 → after the roll 23 → reduced 25% (DamageReductionPercent) → lands 17`.
- Mid-battle: 25 → 0 — the very next hit is the game's again (`[config] DamageReductionPercent: 25 → 0 (source: MCM)`); back to 25.
  *Damage randomness* off: hits stop varying but still land 25% lighter (`~[damage] not rolled - switched off (DamageRandomEnabled): …, 40, reduced → 30`). Back on.
- Summary: `damage reduction (DamageReductionPercent 25%, after the roll): N hits (…); damage the game's A → after the roll B → landed C (-25.0% from the roll's, …% from the game's), …; not reduced: fall damage …`
  — **-25% from the roll's** (±0.5 a hit of rounding), `0-25%` written as `0-25% (changed mid-battle)` only if you moved it.
- Tell Claude how it FEELS: slower without being a slog? Is 25 right, or 20 / 30?

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

**D5. Battle pace — the shield wall swings less (step 21, ~15 min).** Anton's ask: "make the infantry more
defensive, especially the guys with the shields, so that maybe they swing 30% less". The AI's pause after
each swing gets a share on top of tiredness - fresh men wait too, guard up. The same custom battle twice
(D4's rules: the same map and armies, charge and leave them alone, note the clock time at each start):
**infantry against infantry, both sides with shields** (e.g. 100 v 100 legionaries / Sturgian spearmen with
shields, no archers), plus a few two-handed troops on one side if you can (Vlandian voulgiers, Sturgian
heavy axemen).
1. **Run 1 - the defaults**: MCM → *Battle pace (AI)*: *Shield infantry swing less (%)* 30, *Other infantry
   swing less (%)* 15, *AI's own gap between swings (s)* 1.0.
2. **Run 2 - off**: before the battle both *swing less* sliders to **0** → Done. The same battle. Put them
   back to 30 / 15 after.
- **Look for** (run 1 against run 2): fresh shield men trading blows **visibly slower** - a beat with the
  shield up between swings; the shield wall holds longer; the melee lasts longer (the header's `after N s`).
  Nobody should look frozen: a man waiting still blocks, parries, moves.
- **The lines that settle it** (each run's `[summary]`, appendix L6c):
  - `battle pace - shield infantry (…; ShieldInfantrySwingsLessPercent 30 - asks 30% fewer swings): N attacks by
    M men; … cycle C s (n …) = X swings a minute per man while fighting; his own gap after a pause avg G s …;
    the model …: with the setting at 0 A s = … a minute, with it B s = … a minute (30% fewer) - measured ÷ it
    P%: on target` → **run 1's X ÷ run 2's X near 0.7** (the real check); within run 1, `measured ÷ it` on
    target and `his own gap` near 1.0 s (the model's `AiMeleeGapSeconds`).
  - Run 2 with the sliders at 0: the same line reads `… with the setting at 0 A s … - measured ÷ it P%: … (at
    0 this checks the model's own gap)` - P near 100% says the 1.0 s gap is right for your battles; if it
    reads e.g. 80% (cycles shorter than the model), tell Claude the number: *AI's own gap* should go down.
  - `battle pace - other foot melee (…; FootMeleeSwingsLessPercent 15 …)`: the same for the men without a
    shield (0.85 of run 2's rate).
  - `battle pace - riders (…; no setting - tiredness only)`: unchanged between the runs.
  - `[rate] battle pace - first shield infantry attack this mission: <name> at … s (AI melee on foot, a shield
    in the other hand; ShieldInfantrySwingsLessPercent 30) → a pause of 0.76 s asked = the tired pause 0.00 s
    + the battle-pace share 0.76 s - his expected cycle (…) 1.78 s ÷ (1 - 0.30) = 2.54 s: 30% fewer swings`
    - one line per class the first time it attacks: the proof each class was read (a legionary as shield
    infantry, a voulgier as other foot melee, a horseman as a rider).
  - L6b's `GUARD` line: `held by the timer` close to `everyone else` (fresh men held now too - they must keep
    blocking).

**D6. Battle pace — archers shoot slower (step 21, ~10 min).** Anton's ask: "make the archers a bit slower
... fire about 30% slower overall". Every AI bow shot is followed by 2.0 s more, a crossbow shot 2.5 s (on
top of tiredness; horse archers too; javelins and slings no). The same battle twice: **an archer-heavy
one** - e.g. 60 archers + 40 crossbowmen a side, a few infantry in front, you as general; order the archers
to fire at will and wait 2-3 minutes before the lines meet.
1. **Run 1 - the defaults**: *Bowmen: extra wait after each shot (s)* 2.0, *Crossbowmen: …* 2.5.
2. **Run 2 - off**: both at **0**. The same battle. Back to 2.0 / 2.5 after.
- **Look for**: fewer arrows in the air in run 1; the volleys thinner; archers still aim and move normally.
- **The lines**: `battle pace - bowmen (…; ExtraPauseAfterBowShotSeconds 2.0 - sized for about 30% fewer
  shots): N shots by M men; … cycle C s (n …) = X shots a minute per man while shooting; his own gap after a
  pause avg G s (n …); the extra inside a cycle avg 2.00 s - if none of it hid in his own gap, without it the
  cycle would be … s = … a minute: at most P% fewer shots …` → **run 1's X ÷ run 2's X near 0.7** - the real
  check; the same for `crossbowmen`. If the rate fell much less (0.85, say), the extra partly hid in their own
  idle time - tell Claude both X's and he sizes the seconds anew (or set them higher yourself).
- `battle pace - thrown and slings (…; no setting - tiredness only)` - unchanged.

**Broken — tell Claude** (D5 / D6): a man who never attacks again (a pause that does not end - `attack rate -
AI timer ends: … still held at mission end` large); `[error]` lines at `rate.class` (the weapon read failed -
every attack falls back to no share); YOUR attacks slowed at full strength (you are never in it - the
recovery bar must stay full at the peak line); a class line reading `no attacks` for troops that clearly
fought (the read put them elsewhere).

**D7. Brace by orders — the lines attack, then brace (step 23, ~15 min).** Anton's ask: "make soldiers not
swing but only defend when they have reached a certain floor, that depends on their current orders, until they
have replenished the floor +20%" and "when defending they whip out their shields". An AI man whose bar (its FILL -
points ÷ his full pool) falls to his formation's order floor stops swinging, guard up, shield out and up, until he
is back to floor + 20 (± his own 5, rolled once a battle): **hold / halt / retreat / move / follow 60 → 80,
advance 40 → 60, charge 20 → 40** (*Brace by orders (AI)* in MCM). Bows, crossbows and throws go on. Never you.
**A hold-then-charge infantry battle**: custom battle, 80 v 80 infantry (shield men and two-handers mixed - e.g.
Imperial Legionaries + Menavliaton a side), you as general, VerboseLogging on.
1. **Hold**: F1 → *Hold* (your infantry halts). Let the enemy come to you. Watch your line fight: at first they
   swing, then more and more stand with their shields up and do not swing. **Hold ALT** now and then: the numbers
   under each formation marker - the bar falls, and a line near 60% is a line that braces. Two-handers with a
   sword or axe as their other weapon should switch to it and a shield if they carry one.
2. **Charge** after about a minute of fighting: F1 → *Charge*. The men who braced at 60-80% start swinging again at
   once (their floor is 20 now); watch them surge, then brace again much later, near empty.
3. **Advance** in a second battle (optional): F1 → *Advance* - the floor is 40.
4. **The enemy** braces on its own team AI's orders - an AI line advancing into you uses the advance floor (40),
   a holding one 60.
- **Look for**: tired men with their shields up and not swinging, not a whole line frozen for minutes; archers
  behind still shooting; men who were holding a two-hander switching to a sword and shield when they brace (and
  back when it ends - their own AI picks); YOUR attacks never held by it.
- **The lines**: the first brace in full - `[brace] first brace this mission: <name> at … s - order stop / hold (the
  hold and the rest floor 60%), his bar 59.8% of his full pool (… points) → no melee attacks, guard up, until 81.3%
  = floor 60.0% + his margin 21.3 points (BraceRecoverPercent 20 + 1.3, his own roll); shield: …` and its end `[brace]
  first brace ended at … after … s - refilled to floor + recover; …; melee hits taken H, blocked B; his attacks taken
  out F frames, the shield held up R frames, engine calls C`. Then the `[summary] brace …` lines (appendix L6d):
  **`brace lengths`** (most braces short; a big `60 s or more` and `still bracing when the battle ended` =
  lines that turtle - blocks cost Athletics (step 22) and stop the refill, so a man pressed hard may never get back
  to his target; tell Claude), **`brace ends`** (`the order changed` > 0 after your charge), **`brace shield`** (`the
  shield seen in his hand after we asked` vs `the AI put it away again` - how well the shield sticks), **`brace
  GUARD`** (blocked % while bracing against the AI holds GUARD line's `everyone else`; `melee attacks that started
  while bracing anyway` about 0).
- **The ready count (step 24, Anton's ask: "some indication above the squad of ready men")**: while your line holds
  and braces, **hold ALT** - under each marker's bar a third line `ready 34/50`: the men NOT bracing out of the
  formation (you are not counted, so under your own formation's marker it may read one fewer than the game's count
  above). Plain white while most are ready, **yellow** once three in four or fewer are ready, **red** at half or
  fewer. Watch it fall as the hold wears on, then **jump back up** the moment you order the charge (their floor drops
  to 20). The enemy's markers show theirs too. **Open the orders menu** (it slows time): the same `ready 34/50` under
  each card's bar, left; the cell is one row deeper, so the bottom card of each column has its numbers lifted a
  little onto the card's lower edge (only while the ready line is there). Switch *Brace by orders (AI)* → *Tired AI
  brace by their orders* off: the ready lines go at once (nobody braces), the strip is its old size; on again: back.
  *Orders menu strip* → *Show the ready count* off / on does the same for both places; *Ready count yellow / red at
  or below (%)* move the colours live.
- **A/B (optional)**: *Tired AI brace by their orders* off - the same battle: men swing to empty (the step 13 pause
  alone slows them).

**Broken — tell Claude** (D7): a whole line that never swings again for minutes while not being hit (look at
`brace lengths`); `braces the engine never called our input hook during` > 0; men bracing with a bow in hand and
not shooting; YOU unable to attack at a low bar (the brace is never on you - your own pause is the recovery bar);
`[error]` lines at `brace.*`; the shield never out (`the shield seen in his hand after we asked 0` while `taken out`
is large); a ready count that never moves while men visibly brace, or reads more than the formation's size; the
orders strip's ready line overlapping the next card or cut off at the screen's bottom.

**D8. Tournaments and duels fight on - the no-formation floor and each man's own floor (step 28, ~10 min).** Anton's
log (2026-10-12): in a tournament 1v1 both fighters got tired and the AI blocked ~95% of his swings - tournament and
arena fighters have NO formation, so they braced on the hold floor 60% (11 of 16 AI braced, 62% of their time, the
longest brace 163 s). Now no formation has its own floor, `BraceFloorNoFormationPercent` **20 → 40**, and every AI
man's floor carries his own roll of ±`BraceFloorSpreadPercent` **10** (once per man and battle, apart from the ±5 on
the margin) - in every battle, so a holding line no longer braces as one man either.
1. **A tournament** in any town (a few rounds, VerboseLogging on). Fight your opponent. He should swing on well below
   half his bar, then brace (shield up, no swings) only near the end - around 10-30% - and come back about 20 points
   later. A round should end in a fight, not a wall of blocks.
2. **An arena practice fight** (optional): the same - everyone in no formation.
3. **A field battle** (optional, any D part): a holding line now braces man by man over about 20 points of bar (50-70)
   instead of all at 60.
- **The lines**: `[brace] mission start: ON - … is at or below his formation's order floor ± his own 10
  (BraceFloorSpreadPercent, rolled once per man and battle, apart from the margin's roll) … no formation (tournaments,
  arenas, duels) 20% → 40% (BraceFloorNoFormationPercent); …`. The first brace: `[brace] first brace this mission: <name>
  at … s - order no formation (the no formation floor 20%; his own floor 23.4% = + 3.4 points by his own roll,
  BraceFloorSpreadPercent 10), his bar 23.1% of his full pool … until 44.9% = his floor 23.4% + his margin 21.5 points …`.
  The summary: `brace: … by floor: charge 0, advance 0, hold and the rest 0, no formation N; time bracing … = X% of the AI
  fighters' time on the field …, the longest … s` - **X far below the 62% of Anton's log, the longest well under 163 s**;
  and `brace floors (BraceFloorSpreadPercent 10, step 28): N men rolled, their floor offsets now min -9.x / avg ~0 / max
  +9.x points on the order's floor; braces by his own floor: low (−, fights on longer) …, middle …, high (+, braces
  sooner) …`.
4. **Block your way back** (step 29, Anton 2026-10-12: the same 1v1 log showed both men stuck at empty, because every
   paid block restarted the 3 s refill delay). In the tournament, tire yourself out on swings, then only DEFEND for
   ~30 s: block the right way or parry. **Your bar climbs while you block** - each clean block dips it (~0.75 a block,
   ~1.5 a parry as a hero) and it climbs on; a 100-point hero parrying every 1.5 s nets about +1 a parry, ~+40 a
   minute. Then block the WRONG way on purpose: the climb **stops** while you keep doing it. Your opponent recovers the
   same way while he braces - a round should not end with both of you at 0.
- **The lines** (step 29): `[athletics] YOU: weapon parry at … - the first of this kind this battle: … once per blow, and
  the refill keeps running (only a WRONG-side block restarts its delay - OnlyWrongSideBlocksStopRefill); …`; the
  mission-start / summary settings sentence `…; only a WRONG-side shield block restarts the refill delay - a right-side
  block or a parry costs but the refill keeps running (OnlyWrongSideBlocksStopRefill), …`; the blocks summary's `| the
  refill delay restarted by W (right side 0, WRONG side W, parries 0), the refill kept running under K (…)`.
- **Tune live** (MCM, *Brace by orders (AI)*): *Brace floor: no formation* 0 = duel opponents never brace until empty;
  *Brace floor spread per soldier* 0 = everyone exactly on his order's floor again. (*Athletics*) *Only wrong-side
  blocks stop the refill* off = step 22 (every paid block stops it) - the deadlock should come back.
- **Note**: a SOLDIER with a small bar (the floor, 50) who parries every 1.5 s still sinks (1.25 refilled vs 2 paid per
  parry) - by design, the rates decide; right-side shield blocks (1) let even him climb. Tell Claude if duels still
  stall.
5. **The tired opponent guesses wrong** (step 30, Anton 2026-10-12: "the more exhausted an AI man is, the more his block
   direction is random"). In a LATE tournament round (the AI's level is doubled in the final - in vanilla that opponent
   never guesses), get your opponent tired: keep him busy blocking and swinging until his bar is well down (the ALT
   label or the verbose `[athletics]` lines). Then attack from varied directions. Fresh, he reads you; halfway down
   about half his blocks should go the wrong way (still right 1 in 4 by luck); near empty, most of your swings from a
   new direction should land or hit the wrong side of his shield (a wrong-side block costs him 5 and stops his
   refill - he spirals; you, blocking cleanly, recover). It should feel like a tired man, not a wall.
- **The lines** (step 30): `[guard] mission start: tired block direction: on - below the peak line an AI man's block
  direction is partly random: weight w = 100% × (1 − f) (TiredBlockRandomPercent), so at f 0.5 50% of his picks and
  empty 100% are a random direction …`; the first raised chance in full: `[guard] first tired block direction this
  mission: <name> at … s, f 0.52 - the game's random-direction chance for him -1.10 (below 0: never random - counted as
  0) → 0.48 = own 0.00 + (1 − own) × w, w = 100% × (1 − 0.52) = 0.48 - …`; the summary's two lines (L6e): `tired block
  direction (…): the game's random-direction chance raised for N AI men …; his own → written, avg by f band: …` and
  `AI guard by f (…): peak (f 1) … blows - blocked X% (…), landed …; … empty (f 0) … blows - blocked Y% (…)` - **Y well
  below X**, with more `WRONG side` and `landed` in the low bands. **Read the split (step 34)**: `AI guard by f WITH a
  shield in hand` - a tired shield man's wrong guess is mostly a `WRONG side` block (the shield still stops it, landed
  stays low); `AI guard by f WITHOUT a shield` - THE line for "do tired men parry worse": its empty band's `landed` should
  be far above its peak band's (a wrong-direction parry is no parry).
- **Tune live** (MCM, *Tired fighters*): *Tired AI blocks in a random direction (%)* 0 = the game's own blocking (the
  95% wall should come back in a final round), 50 = an empty man guesses half his blocks.

**Broken — tell Claude** (D8): a tournament summary with `hold and the rest` braces and no `no formation` ones (the
order read sees a formation there); `time bracing` still near half the AI's time in a 1v1; floor offsets outside ±10.
Step 30: the `AI guard by f` empty band blocking about as well as the peak band with `tired block direction` raised for
the men who fought there - the engine ignores the game's own random-direction chance, and the fallback (his direction
bits replaced in his input, AI_NOTES "Step 30") is next; step 34: `AI guard by f WITHOUT a shield` landing about as
often at empty as at peak (with a few dozen blows in each band) - the shieldless AI is not guessing wrong; no `[guard] first tired block direction` line in a battle where
AI men got tired (the decorator is not on top - see the `[speed] stat model on top` line).

**D9. The swing stack — save swings up, then burst (step 32, ~10 min).** Anton's design: "chain swings stack" - one slow
swing every few seconds is read and blocked; now each fighter stores swings (his Athletics points ÷ 20, rounded, at least
2) and waits only when none is left. Custom battle or the arena; you on foot with a one-hander (or a tournament round).
1. **Look at the bar.** The Attack recovery bar's words now read **"Swings 8/8"** (a 150 bar full) - fewer as your bar
   falls (50%: 4, empty: 2). At full strength it never runs down below the cap for long (the pause there is 0).
2. **Burst.** Get to about half your bar (swing at the air), stand still a few seconds until the count is full, then
   hammer attack: every stored swing goes out back to back with no wait, the count falling "4/4 → 3/4 → … 0/3"; at 0 the
   bar empties and fills toward the next, the seconds inside it. Keep the button held: the next swing fires the moment
   one is back ("1/3").
3. **Kick at 0.** With "0/N" press the kick key: nothing happens (the press is simply gone - only an attack press
   flashes the bar); with a swing stored the kick works and spends it ("2/3 → 1/3").
4. **Save up.** Back off and block (clean blocks are rest) for a few seconds: the count climbs one per pause. Then strike
   a burst into an opponent's guard - against a tournament opponent a burst of 3-4 should land far more than one swing
   every ~3 s did.
5. **The AI**: watch a tired AI man in a 1v1 - he now strikes a few swings in a row, then waits; his pauses come between
   bursts, not after every swing.
- **The lines**: `[rate] swing stack (step 32) at mission start: ON - every fighter stores swings: max(2, his Athletics
  points / 20, rounded) …`; `[athletics] YOU: first burst this battle at … s - a second attack straight after the last
  from your stored swings (3/4 left; one comes back per … s you are not attacking)`; `[athletics] YOU: swing stack empty at
  … s - you spent your last stored swing (1 → 0 of 3 on … points) on an attack; the next comes back in … s of not
  attacking - until then your attack button and kick key do nothing …`; `[athletics] YOU: first attack pause this battle
  at … → no new attack for … s - your swing stack is empty: the next stored swing comes back after that much time not
  attacking …`; `[athletics] YOU: kick pressed at … s with no stored swing (0/3) - refused: the engine never saw it
  (KicksUseSwings) …`; `[hud] recovery bar: first empty swing stack shown at … - "Swings 0/3", …`. The summary (L6f):
  `swing stack - you: attacks N (from the stack A, after waiting at 0 W, at an empty stack 0); bursts (2+ in a row, no swing
  back between) B by length …, longest L; waits at 0 W (avg … s …)`, the same for the AI, `swing stack - kicks and bashes
  (…): spent a swing K (you k), refused at 0 - yours R (the gate took the kick key out), the AI's F input frames; started
  at 0 anyway 0 …`, `swing stack - average fill (stored ÷ his cap) by f: …`, and in the attack-rate block `attack rate -
  AI timer, not held: … a stored swing left (step 32's swing stack) N` (most AI attacks).
- **Tune live** (MCM, *Tired fighters*): *Athletics points per stored swing* 10 = twice the stack, 40 = half; *Fewest stored
  swings* 1 = an empty man waits after every swing again; *Kicks and shield bashes use a stored swing* off = kicks free of
  the stack; *Swing stack* off = the pause after every attack (the old way) - an A/B in one battle.
- **Read the verdicts with it**: the `attack rate` band rows still compare each band's cycle with its target; with the
  stack a band full of bursts reads "too fast" and the waits after them "too slow" - over the fight they even out.

**Broken — tell Claude** (D9): `at an empty stack` more than a few for you (the gate let an attack through at 0); a kick
going out at "0/N"; the count not falling during a burst or not climbing while you rest; the held button not firing when a
swing is back; the AI's `battle pace` attacks a minute per man clearly higher than in a battle with *Swing stack* off (the
AI's own gap is refilling - AI_NOTES "Step 32"); `the AI's 0 input frames` with AI men seen kicking at 0 (the AI's kick is
not in the input we edit - expected, note it).

**D10. The sprint — a dodge for you, sharper charges for the AI (step 33, ~15 min).** Anton's ask: "a short burst of
speed, meant to feel a little like a dodge, and for the AI to make charges sharper". Your store = your Athletics points ÷
20 in seconds (150 = 7.5 s), **never below 2 s** (step 35); hold **Left Ctrl** to sprint at **2 × your FRESH run speed**
(step 35 - whatever your Athletics) for the first 30% of it, then easing to 1.1 × your CURRENT run; top speed in **1 s**
while sprinting (the game's own foot acceleration is ~2.3-2.8 s); 20% of the store is needed to start; it refills in 7 s
standing fresh (half moving, half again tired). Step 35 came from Anton's two tournaments of 2026-10-12: at 1.6 × the
current run his 0.4-0.8 s taps never got past the game's slow acceleration, and a tired store had shrunk to nothing.
1. **Field battle, on foot.** Before the lines meet, look above the Attack recovery bar: **"Sprint 7.5 s"** (a 150 bar),
   a blue bar with a white mark at 20%. Hold Left Ctrl and run: the bar turns **yellow** and drains only while you move;
   you should clearly outrun your own men for ~2 s - twice their pace, reached within a second of pressing - then ease
   off. **Tap** Ctrl for half a second: you should feel the lunge at once (step 35). Let go after 3 s - the rest stays
   ("Sprint 4.4 s").
   Stand still: it refills (7 s from empty at full strength). Run dry, then tap Ctrl at "Sprint 1.0 s": nothing (grey,
   below the mark).
2. **Watch the feet.** At 2 × do the legs keep up, or do the feet slide? Does a skilled hero (big Athletics) reach a
   speed that looks silly? Does the 1 s acceleration look like a jolt? Note it - the engine's own animation is the open
   question (RESEARCH §N).
3. **Ctrl conflicts.** Hold Ctrl with the order menu open, in the Escape menu, in a conversation: no sprint. In free
   camera (RTS Camera / deployment) Ctrl + wheel changes the camera speed - the sprint is off there (not your hands).
   Note anything else Ctrl does in a fight.
   **In a town (2026-10-14):** walk about with nothing drawn - no sprint bar; draw a weapon - it appears; hold Ctrl - you
   sprint and the bar stays up while it refills. Pick a street fight with a gang: the sprint works there too. Note anything
   Ctrl does in town in vanilla.
4. **Order a charge.** Put an infantry formation on hold ~60 m from the enemy, then F1 → F3 (charge): the men should
   surge for their first metres and slow back to a run; with ALT the formation reaches the enemy noticeably sooner. An
   advance (F1 → F4) sprints them down to half their FULL store (step 37). A second charge order on a charging formation does
   nothing new.
5. **A tournament.** Ctrl works in the arena too (a dodge out of a corner). The AI there has no formation orders, so it
   never sprints.
6. **Tired** (step 35): fight until your bar is empty, then sprint: **"Sprint 2.0 s"** - the floor - and the burst starts
   just as fast as a fresh one (2 × a fresh run), then fades all the way back to your tired jog within the 2 s. Run dry and
   stand: the 2 s refill takes ~14 s at an empty bar (28 s moving). While you sprint your Athletics bar does not refill.
7. **Shock troops** (steps 36 / 37, Anton's case - one reserve per order): put a fresh infantry formation on hold, then
   walk it round behind the enemy with three or four plain **move** orders (F1 → F1, a spot each time) a few seconds apart.
   The first move: a short dash (1 s of a 5 s store, 20% - down to the 80% reserve); the next ones right after: none -
   they jog (a man refilled to 90% dashes 10%). Now F1 → F3 (charge): they sprint everything left (about 80% of the
   store) into the enemy's back. An advance (F1 → F4) from a full store sprints half of it (from 90%: 40%, from 60%:
   10%, at or below 50%: none).
- **The lines**: `[sprint] mission start: ON - hold LeftControl (on foot, in a fight); the store = Athletics points / 20 s
  (150 points = 7.5 s, never below 2.0 s (an empty bar still holds 2.0 s)), a start needs 20% of it; speed x2.00 of his
  FRESH run speed (whatever his Athletics) for the first 30% … then a straight line to x1.10 of his CURRENT run speed …; top
  speed reached in at most 1.0 s while sprinting (the game's own acceleration: ~2.3-2.8 s on foot) …`;
  `[sprint] YOU: first sprint this battle at … s - stored 7.50 s of 7.50 s (150 Athletics points / 20.0), so it lasts T
  7.50 s of moving: x2.00 of your FRESH run speed (the run property x1.00 → x2.00) until 2.25 s, then a straight line to
  x1.10 of your current run (x1.10) at 7.50 s; top speed reached in 1.00 s instead of the game's 2.6… s …` (tired: `(0
  Athletics points / 20.0 = 0.00 s - the store's floor SprintMinSeconds 2.0 s holds it up)` and `(the run property x0.30 →
  x2.00) … to x1.10 of your current run (x0.33)`); `[sprint] YOU: sprint ended at … s (key released) after … s of moving - … s left stored`;
  `[sprint] YOU: first sprint refused this battle at … - … below the 20% a start needs …`; `[sprint] first new order
  seen this battle at … s: formation slot N Stop → Charge (charge: its AI men on foot sprint what they hold
  above a 0% reserve)`; `[sprint] first AI order sprint this battle: <name> at … s on a new charge order - stored 5.00 s
  of 5.00 s (100%), charge keeps 0% → asked T 5.00 s (stored - keep x 5.00; left after it 0%) …` (step 37; a move: `move
  keeps 80% → asked T 1.00 s … left after it 80%`); `[sprint] first AI sprint held back by a reserve this battle: <name>
  at … s on a new move order - stored 4.00 s of 5.00 s (80%), move keeps 80% - nothing above the reserve, no sprint (it
  stays for the next order)` (step 37); `[hud] sprint bar: first values pushed …`, `[hud] sprint bar: first sprint shown …`. The summary (L6g):
  `sprint - you: N sprints …`, `sprint - AI: new orders seen - charge C, advance A, move V, other O; sprints - charge (keeps
  0%) M men (… s in all, avg … s), advance (keeps 50%) …, move (keeps 80%) …; not sprinted: reserve kept N (charge 0,
  advance A, move N), less than 0.2 s or empty: …` (step 37), and the speed check `sprint speed check (x his FRESH run speed = his top at the sprint's start ÷ his
  run multiplier then, while it ran): N samples - asked avg x1.6…, engine top avg x1.6… - the engine's top speed follows
  the sprint - no cap above the run speed; acceleration while sprinting: the game's top-speed reach avg 2.5… s → ours 1.00
  s (sprint starts: N)`; with a tired fight also `N started off the 2.0 s floor` (you) and `off the 2.0 s floor N` (AI).
- **Tune live** (MCM, *Sprint*): *Sprint speed at its start* 1.5 = a gentler dodge, 2.5 = a lunge; *Sprint acceleration*
  0.5 = snappier, 0 = the game's own (compare a tap with and without); *Smallest sprint store* 0 = step 33's (an empty bar
  cannot sprint), 3 = a bigger tired dodge; *Full-speed part* 100 =
  no easing; *Athletics points per sprint second* 10 = twice as long; *AI sprint on a move: keeps* 0 = a move sprints all of
  it like a charge, 95 = moves almost never sprint; *AI sprint on an advance: keeps* 100 = only charges and moves (step 37);
  *Sprint key* - type another name (e.g. `X1MouseButton` for a side mouse button); *Sprint* off = gone at once.

**Broken — tell Claude** (D10): a tap still feeling like nothing with *Sprint acceleration* 1 (then the engine ignores
TopSpeedReachDuration for a running man - AI_NOTES "Step 35"); the speed check `does NOT follow (a native cap?)` or engine ≈ x1.00 while asked x1.4+
(the engine caps the run property above its base - the sprint would need another lever); you visibly not faster while the
bar drains; Ctrl doing something else in a fight; an AI charge with `sprints - charge 0 men` after a `new orders seen -
charge` (the men were not read in that formation); AI men sprinting on a hold order; step 36: shock troops NOT sprinting
on the charge after several moves (`reserve kept` counted on the charge, or `sprints - charge 0 men`), a move sprinting
more than 20% of the store (or below the 80% reserve), an advance below half, or `reserve kept 0` after many back-to-back moves; `[error] … sprint.` lines.

**D11. The shield wall holds its slots (step 41, ~10 min).** Anton's report (2026-10-17): a fresh shield wall waiting for
the enemy - "men step 2-3 m forward out of the wall to swing at an enemy, then walk back to their slot ... every man does
this, so the wall bulges and breaks". The mod now tells every AI man of a shield wall / square / circle to keep his slot
(the engine's own "keep with the formation" flag, which vanilla only ever sets for shield men of ARCHER formations).
1. **Your test, ON.** Custom battle, infantry against infantry (40 v 40 is plenty, shields on your side, nobody tired).
   Put your infantry in a **shield wall** (F2 → shield wall) on the spot and let the enemy come to you. Watch the front rank
   from the side or above as the enemy hits it. **You should SEE**: the wall stays a straight wall; the front rank swings
   at what reaches it, from its place; nobody walks 2-3 m out and back; the line does not bulge where the fighting is.
2. **The same, OFF, in the same battle.** Escape → MCM → *Battle pace (AI)* → *Shield walls hold their slots* off → Done
   (the log notes the time). Watch the same wall for a minute: **you should see the old behaviour come back** - men
   stepping out at their enemies and walking back. Switch it on again and watch them settle back into their slots. (Or
   fight two battles, one each way - the summary splits the samples either way.)
3. **Square and circle** (optional): the same with F2 → square / circle - they hold too.
4. **A charge breaks it on purpose**: F1 → F3 on the shield wall - the men run at the enemy as before (a charge is never held).
5. **Things to note**: a man who ends up far behind the wall (pushed, or late) should run to rejoin; the wall should still
   turn and follow a move order (F1 → F1) as a wall; you and your bodyguard on foot are never held.
- **The lines**: `[wall] mission start: ON (ShieldWallHoldsSlots) - every AI man on foot in a shield wall, square or circle
  keeps his slot instead of walking at the enemy he fights …`; `[wall] first man held in his slot this mission: <name> at
  … s - his formation in a shield wall under the order move to a position; his slot (…), he stands … m from it (… m ahead
  along the facing …); the formation's speed … m/s …- "keep with the formation instead of moving to the agent" written TRUE
  (vanilla's update timer read: he is rewritten right after each of vanilla's own updates)`; the switch: `[wall]
  ShieldWallHoldsSlots switched OFF mid-mission at … s: N men given vanilla's own value back at once …` and `… switched ON
  mid-mission at …`. The summary (L6h): **compare `wall slots ON` with `wall slots OFF (vanilla)`** - the share "more than
  1 m ahead of his slot" and "more than 2 m", above all **in contact** (an enemy within 5 m): ON should be far lower.
- **Tune live**: *Shield walls hold their slots* is the only setting (on / off).

**Broken — tell Claude** (D11): the wall still bulges with the switch on and `wall slots ON` ≈ `OFF` in contact (the
engine ignores the flag for infantry - another lever is needed); the front rank no longer swings at men right in front of
it (held too hard); `men whose update timer could not be read` above 0, or `[wall] vanilla's update timer … cannot be read`
(a game update - the fallback runs); men in a LINE formation held (they never should be); `[error] … wall.` lines.

**D12. The shield wall's small step out (step 42, ~10 min - right after D11, the same battle is fine).** Anton's playtest of
step 41 (2026-10-17): the wall held, but the front men "looked many times like they were about to reach to hit but stopped,
maybe as the enemy took a few steps back, so they very rarely hit". Now a held man may follow an enemy who backs off just out
of reach - up to *Shield walls: a step out to strike* metres (1.0) out of his place - strike, and is held again and walks back.
1. **Your test, L 1 (the default).** Custom battle, infantry against infantry, your infantry in a **shield wall** on the spot,
   the enemy comes to you. Watch the front rank from the side when the enemy recoils or backs off a step. **You should SEE**:
   a front man takes ONE short step after him and swings (hits land), then walks back into his place; the wall stays a wall
   - small dents of about a metre that close again, nobody 2+ m out, nobody chasing.
2. **L 0 - the hard hold (step 41) - live**: Escape → MCM → *Battle pace (AI)* → *Shield walls: a step out to strike* 0 → Done.
   **You should see** step 41 again: men stand fast and reach at enemies who back off without following.
3. **L 2 - live**: the same, 2. **You should see** longer steps (a looser wall), still no chasing beyond ~2 m, everyone walking
   back. Pick the number you like - 1 is the starting guess.
4. **A square** (optional): F2 → square; men on the SIDES and the BACK step outwards on their own face, not toward the front.
5. **Things to note**: a man who stepped out comes back before he steps again (no man creeping forward step by step); a charge
   (F1 → F3) still releases everyone as before; *Shield walls hold their slots* off = no leash at all (vanilla).
- **The lines**: the mission start's sentence ends `… a held man may step up to 1.0 m out of his slot at an enemy close in
  front of it (within 1.0 + 2.0 m of the slot), then is held again and walks back - he may step again once back within
  0.30 m of his slot (ShieldWallStepOutMetres)` (L 0: `no step out (ShieldWallStepOutMetres 0 - the hard hold of step 41)`);
  `[wall] first step out of the wall this mission: <name> at … s - his enemy … m out of his slot (close: within the leash
  1.0 + 2.0 m of the slot), he stood … m out; …` and `[wall] first step out ended: <name> at … s - reached the leash: the
  farthest … m out of his slot (the leash 1.0 m, … m past it), … s after it began - held again …`; a slider change:
  `[wall] ShieldWallStepOutMetres 1.0 → 0.0 m mid-mission at … s: …`. The summary (L6h): `wall step out (step 42): …`.
- **Tune live**: *Shield walls: a step out to strike (m)*, 0-3.

**Broken — tell Claude** (D12): men still stop short and rarely hit with L 1 (`wall step out` shows steps, but the eye sees
no blows - the native AI may need more than the flag); men chase beyond the leash (`in contact past the leash + 0.5 m` well
above a few %, or `wall slots ON` "more than 2 m" climbing back toward OFF's); `0 steps out` in a battle where the enemy
clearly backed off in front of the wall (the target read sees nobody); a step rate in the dozens a man a minute (flickering
at the edge); square side men stepping toward the formation's front; `[error] … wall.` lines.

**D13. Eagerness by Athletics, and the fever of a charge (step 43, ~25 min).** Anton's idea (2026-10-17): men are defensive
by nature and a man at work doesn't hold a pace until he drops - the more tired, the less he swings; but when the general says
NOW!, everybody surges, longer for veterans, shorter for the tired; the surge fades. Now an AI man on foot decides to attack at
60% of the game's eagerness with a full bar, sliding to 20% at an empty one; a NEW charge / advance order lifts him to 80% for his
current Athletics ÷ 10 seconds (the clock starts at the first enemy within 5 m; a shield in hand burns it in 70% of the time),
fading back to his tired eagerness. A swing costs 3 Athletics now (was 10), a wrong-side shield block 2 (was 5) - an old
config.json holding 10 / 5 is moved once (`[config] migrated config.json … CostPerBlow: 10.0 → 3.0 (format 7 → 8 …`).
1. **A field battle (your test).** Custom battle, infantry against infantry (60 v 60, no archers). Hold your line (F1 F1 or
   stand) and let them come; watch the first minute of contact. **You should SEE**: the lines meet and SPAR - blocks, parries,
   a swing now and then - not a flurry of blows until men drop; as men tire they swing visibly less. Then **F1 F3 (charge)**:
   your men surge - more swings at once, a few sprint the last metres - and after ~5-10 s of contact they settle down again.
   The enemy general's own charges / advances do the same to his men.
2. **Does it slow down without deadlocking?** Let a fight run 2-3 minutes. Tired lines should still trade blows (fewer, not none);
   two spent lines should not stand parrying forever. Watch with the brace floors in mind: holding men stop at 60% of their bar,
   advancing 40%, charging 20% - a holding line now braces later (cheaper swings) and swings less while it lasts.
3. **A tournament** (or an arena fight): no orders, no fever - only the bar's eagerness (the no-formation brace floor is 20%).
   **You should see** calmer duels - feints, blocks, a measured exchange - that still end; a tired fighter swings rarely.
4. **Live, in MCM** (*Aggression and fever (AI)*): *AI aggression at a full bar* 100 and *… at an empty bar* 100 = the game's
   own AI (compare the same fight); 60 / 20 back; try *Fever aggression at its peak* 100 (a charge at the game's full eagerness)
   or 60 (the fever adds nothing to a fresh man); *Fever on a charge or advance order* off.
- **The lines**: `[fever] mission start: aggression: AI men on foot decide to attack at 60% of the game's rate at a full bar,
  sliding in a straight line to 20% at an empty one (…) - attack chance on a charge 0.144 → fresh 0.086, half 0.058, empty 0.029,
  fever peak 0.115 …; fever: a NEW charge / advance order arms …; fever sprint: every 1.0 s …`; `[fever] first decision values
  written this mission: <name> (bar 100%, x0.60) - attack chance 0.096 → 0.058, …`; `[fever] first fever armed this battle:
  <name> at … s on a new charge order (yours | the AI general's, Charge) - N Athletics points / 10.0 = … s of fever; his attack
  decisions x0.80 the game's (his tired share at a …% bar: x0.…) until an enemy comes within 5.0 m, then the clock runs`;
  `[fever] first fever burning …`, `[fever] first fever sprint …`, `[fever] first fever faded …`. The summary (L6i): ten
  `fever …` lines.
- **Read first**: `fever proof:` - AI melee attacks per man-minute in contact by bar band 100-75 / 75-50 / 50-25 / 25-0 %: the
  `no fever` row should FALL from the first band to the last (roughly 60 : 50 : 40 : 30 : 20 of each other, on top of the pause),
  the `in a fever` row should sit above it; `fever proof, no formation` the same for a tournament; `fever values written` the
  numbers that really went in (bar 100-75% x~0.55-0.60, … in a fever x~0.65-0.80); `fever recomputes` a few a man-minute.

**Broken — tell Claude** (D13): the `no fever` rate does not fall with the bar (the native AI ignores the values - RESEARCH
UNVERIFIED 25); `in a fever` no higher than `no fever`; `0 of N fevers met an enemy` after a real charge; fevers never armed
after F1 F3 (`fever armed: 0`); a deadlock (two lines facing each other, nobody swinging, for a minute); `fever recomputes` in the
dozens a man-minute; `[error] … fever.` lines.

**D14. The fever mark under ALT (step 44, ~10 min - right after D13, the same battle is fine).** Anton's ask (2026-10-17): when
I hold ALT the game shows a small icon above each of my soldiers - show me there which of them are in FEVER. Now, while the
game's formation markers show (ALT held, or the orders menu open), each man in a fever gets a small bar of ours above his head
(just over the game's own icon): **bright orange** and full while he is armed (running at the enemy), the fill shrinking and the
colour fading through **yellow** as the fever burns after the first clash, gone when it is over. Calm men and you: nothing.
1. **Your charge.** Custom battle, 60 v 60 infantry. Hold ALT before any order: **no marks** (nobody is in a fever). Give
   **F1 F3 (charge)** and hold ALT as they run: **every charging man has a full orange bar** above his head, riding him as he
   runs. At the clash, keep ALT down: within a few seconds the bars of the men in contact start to **shrink and turn yellow**;
   ~10 s later (a fresh man; less for a tired one or one with a shield) they **vanish** one by one; the men still marching to
   the enemy keep theirs full.
2. **The enemy's charge.** When the AI general charges / advances, hold ALT: **his charging men are marked too** (the game's
   own icon never marks the enemy - ours does). MCM → *Hold ALT: formation markers, Athletics and fever bars* → *Enemy fever marks too*
   off: theirs go at once, yours stay; on again.
3. **Close and far.** Walk into your own charging line with ALT held: the bar fades out within ~8 m and is gone closer than
   1.5 m (like the game's icon). Far men's bars are a little smaller. Open the orders menu (backspace): the marks show there
   too (the formation markers do). F1 F1 (hold) ends the fever: the marks go.
4. **Big battle** (500+ men, a full charge): at most *Most men marked at once* (200) - the nearest ones - and the frame
   rate should not drop when you hold ALT. Try 50 live.
5. **Live, in MCM**: *Fever marks above the men* off / on; *Advanced* → *Fever mark: length / thickness / height above the man*
   (if the bar sits on the game's icon, raise the height; tell Claude the number).
- **Since step 46** every man also has an Athletics bar ABOVE the fever bar (D15); the fever bar is the lower one. To see D14
  exactly as step 44 built it, switch *Athletics bars above the men* off. The log calls the view **`ALT bars`** since step 46 (the
  lines below are as of step 46).
- **The lines**: `[hud] attached: ALT bars (FeverMarkView, movie TraxFeverMarks, prefab installed) - shown while …`;
  `[hud] ALT bars: layer created at … s (mode Battle, …) - movie TraxFeverMarks loaded OK (…)`; `[hud] ALT bars: first bars
  drawn at … s (show #N) - N men looked at (yours and allies N, enemy N), N in a fever (armed N, burning N); N men marked: N Athletics
  bars (green N, …), N fever bars; skipped: behind the camera N, …; screen W x H px, UI scale S; Athletics bars …; fever marks 22 x 5
  UI px …; the first drawn: yours man 412 at (960, 510), 14.2 m, armed, Athletics 100% green | …; source: the combat camera's
  projection …`. The summary (L1 hud block): `hud: ALT bars (movie TraxFeverMarks) - on screen …` and `hud: ALT bars - shown Nx, on
  screen T s over N frames; men marked a frame: mean M, max X; Athletics bars a frame: …; fever bars a frame: mean, max; most men
  looked at in a frame L (yours and allies Y, enemy E), most in a fever F; frames at the cap C; men skipped, summed over frames: …;
  cost N µs a frame (most N µs); errors 0`.
- **Read first**: the summary's `cost` (a few tens of µs a frame expected - plus the game's own drawing of the bars) and `fever
  bars a frame: max` (never above the cap).

**Broken — tell Claude** (D14): no bar on charging men with ALT held but the `[fever]` lines say fevers are armed; a bar that
does NOT ride its man (lags behind or sits at his feet); bars at the wrong height (on the icon, or far above); marks that stay on
after the battle's fevers ended; a bar on YOU; the frame rate dropping while ALT is held in a big battle; `[error] … hud.` lines
or `ALT bars: … DISABLED`.

**D15. An Athletics bar above every man under ALT (step 46, ~15 min - the same battle as D13 / D14 is fine).** Anton's ask
(2026-10-17): "add how tired they are in a bar above that fever bar, maybe a green bar that gets to red, like on the unit
selection / formation screen". Now, while the game's formation markers show (ALT held, or the orders menu open), EVERY soldier -
yours, your allies', the enemy's, riders and lords too, never you - has a small bar above his head: his Athletics left (points of
his whole bar), coloured like the orders-menu strip (green at full strength, blue just below the peak line, yellow, orange, red),
the part his wounds hold dark red. The fever bar (D14) sits just under it while he is in a fever; his Athletics bar stays at the
same height when the fever starts and ends.
1. **Before the fight.** Custom battle, 60 v 60 infantry (+ some cavalry on each side). Hold ALT at the start: **every man has a
   full green bar**, the enemy's too; you have none (your own bar is bottom right). Riders' bars sit above their heads, a little
   higher than the men on foot's.
2. **The charge.** F1 F3 and hold ALT: under each green bar an **orange fever bar** appears; the green bars do NOT move up or down.
3. **As the fight goes on**, hold ALT every 20-30 s: the front men's bars **shorten and turn blue → yellow → orange → red**; men
   in the back stay green; a wounded man's bar has a **dark red end** it cannot refill into. Compare with the orders menu's strip
   (backspace): the colours of a formation's men should match its average there.
4. **The enemy.** MCM → *Hold ALT: formation markers, Athletics and fever bars* → *Enemy Athletics bars too* off: the enemy's bars
   go at once (their fever bars stay while *Enemy fever marks too* is on); on again.
5. **Off switches.** *Fever marks above the men* off: the Athletics bars drop down a little (no fever place under them) - that is
   meant. *Athletics bars above the men* off: D14 exactly.
6. **Big battle** (500+ men): at most *Most men marked at once* (200) - the nearest men ON the screen - and no frame drop while ALT is
   held. *Advanced* → *Athletics bar above a man: length / thickness / gap over the fever bar* live.
- **The lines**: `[hud] ALT bars: first bars drawn at … s (show #N) - N men looked at (yours and allies N, enemy N), N in a fever
  (…); N men marked: N Athletics bars (green N, blue N, yellow N, orange N, red N; wounded N), N fever bars; skipped: …; …; Athletics
  bars 22 x 5 UI px, 2 px above the fever bar (its place kept while he has none), the enemy's too; fever marks …; the first drawn:
  yours man 412 at (960, 510), 14.2 m, calm, Athletics 100% green | …`. The summary: `hud: ALT bars - shown Nx, …; Athletics bars a
  frame: mean M, max X (summed over frames: green …, blue …, yellow …, orange …, red …; wounded W); fever bars a frame: …; most men
  looked at in a frame L …; frames at the cap C …; cost N µs a frame (most N µs); errors 0`.
- **Read first**: the summary's colours (green early, more yellow / orange / red after a long fight) and `cost` (the smoke: 70 µs a
  frame for 1000 men all on the screen, plus the game's own projections and drawing - about 200 a frame).

**Broken — tell Claude** (D15): no Athletics bars with ALT held (and no `first bars drawn` line); a bar on YOU; bars that never change
colour in a long fight while the strip's do; a bar that jumps when a man's fever starts or ends (with *Fever marks above the men* on);
a rider's bar inside his body or far above him (tell Claude how far - `RiderLiftMetres` is 1 m); the frame rate dropping while ALT is
held; `[error] … hud.` lines or `ALT bars: … DISABLED`.

### THE IMPACT MORNING SCRIPT (steps 47-54 - read this first; D16-D23 below are the detail)

> **Turn ImpactSpike OFF after D16** (MCM → Advanced → *Impact test (spike)*). While it is on, the model is in SHADOW and the spike itself
> forces staggers (lever 4, the first block every 10 s) and knockdowns (lever 6, a block every 20 s) on BLOCKERS - a correct block will feel
> punished. That is what Anton felt on 2026-10-18 morning; step 56 (D25) is the model's own answer.

Everything from D16 to D23 was built in one night on assumptions only the game can confirm. Do them **in this order** - each one says
whether the next one can be trusted. After each battle open `trax_combat.log` (beside config.json) and read the lines named here; send
Claude the `[summary] impact …` block of every battle.

| # | What | ~min | Read after it (the `[summary]` lines) | Good = | Bad = (the fallback Claude builds) |
|---|---|---|---|---|---|
| 1 | **D16 the spike** (MCM → Advanced → *Impact test (spike)* ON): a tournament, then a 50 v 50 shield battle; then the spike OFF again | 25 | `impact spike lever 1-6 … → WORKS / FAILS / UNSEEN` | all six WORKS (or UNSEEN - play a bit more) | a FAILS: its line names the fallback (lever 1 → our own knockdown blow; 3 → `SetOverridenStrikeAndDeathAction`; 4 → the longer block stun alone; 6 → a crush-through) |
| 2 | **D19 + D20 + D21's steps 1-3 in ONE tournament** (the spike off, defaults): kick, block, hit a fresh man (nothing), tire him (ALT - his bar orange / red), then kick / swing at his shield / hit him clean; let him tire you | 15 | `impact live …` (kicks), `impact live … blocks …`, `impact live clean hits …`, `impact live shrug-offs`, `impact messages` | the 100-75 band 0 everywhere; `Fall seen` ≈ applied; staggers `seen` ≈ applied, `measured` ≈ asked; the on-screen lines appear (red / purple) | `Fall seen 0` on a family (the engine ignores our KnockDown there); the slowed reactions measure like the untouched ones (the slow does not hold) |
| 3 | **A 50 v 50 shield-infantry battle** (D20 step 5): let the lines grind 5-8 min, hold ALT now and then | 10 | the same lines + `impact momentum`, `impact riders` (none expected), `courage …` | shields on the ground between tired lines, men staggering after blocks; `BLOCKS MADE FROM INSIDE A STAGGER` ~0 | a stutter (`cost` lines), a man frozen / sliding, `levers disabled` anything |
| 4 | **D21 step 4 - the SHOCK CHARGE** (a fresh two-hander unit into the flank of a tired line, then into a fresh reserve, then once with Momentum OFF) | 15 | `impact momentum` - `sprinting` vs `standing`, most of all `on men below half a bar` | sprinting far above standing on tired men; the fresh reserve barely moves | `sprinting 0` although they clearly sprinted (the speed read is wrong) |
| 5 | **D22 - the CAVALRY CHARGE and riders** (a fresh cavalry unit into a tired line; your own horse; a rider you fight on foot) | 15 | `impact riders …`, `impact riders' shields and limits`, the momentum line's `RIDERS` part, `impact messages` | `SEEN off the horse` ≈ APPLIED; riders never stagger; galloping above standing | `still mounted` ≈ applied (the engine ignores our dismount - tell Claude) |
| 6 | **D23 - the AI PUNISHES a window** (a 50 v 50 shield battle ground to orange / red, a few minutes of it with *The AI punishes a staggered or downed man* OFF, then ON again; let a tired enemy break YOUR guard) | 10 | `impact punish windows`, `impact punish proof` (ON vs OFF), `impact punish men` | ON: `target reeling / down` well above `target not`, and above the OFF row's; a good share of windows `PUNISHED`; you get hit while you reel | ON = OFF (the native AI ignores the raised eagerness in that moment - tell Claude: the fallback is a direct attack wish through the input hook) |
| 7 | **D24 - CAVALRY FLOCKING** (step 55 - only once the step-55 build is installed): mixed cavalry on both sides into an infantry fight; watch them tire, wheel off and come back in groups; *Cavalry flocking* OFF for 2-3 min, then ON | 15 | `cavalry flocking`, `cavalry flips`, `cavalry time between passes`, `cavalry lone passes`, `cavalry lever` | fresh: the game's dance; later small waves of 3-5, late big waves; lone passes ON well below OFF; the time between passes ON growing with tiredness | tired riders keep charging alone with `points sent` high (the engine ignores the scripted point on a charging rider - tell Claude) or many `gave up` (riders never rest enough to lead) |
| 8 | **D25 - RIGHT BLOCKS FAVOUR THE DEFENDER** (step 56, the spike OFF): a tournament round to orange / red - block on the right side, then on the wrong side on purpose | 15 | `impact live … blocks …` (by kind × band, the riposte by the attacker's band) | right-side blocks / parries in the red band: a few % staggered; ripostes climbing with the attacker's tiredness; wrong side at 90 / 50 / 40 | a correct blocker staggered often with the spike off; ripostes decided but never applied |
| 9 | **D28 - MORE MEN FALL, THE CHARGE MATTERS** (step 62): looters into your shield wall; heavy infantry into archers' backs; you sprinting into a heavy shieldman's right block | 30 | `impact falls`, `impact direction`, `impact toughness`, `impact charges`, `damage critical` | falls played (game + backup) ≈ decided; rear down well above front; looters thrown back ~1 in 3 sprinting; your red "thrown back" line now and then | `played by the game` ~0 (the backup does all the work); `sprinting at the blow 0`; the archers' rear no higher than front |
| 10 | **D29 - FALLS ON THE HIT, A STRONG REAR BLOW, THE WALL PULLS MEN BACK** (step 63): a shield-wall battle with a deep wall of yours; a unit round the enemy's flank into its back | 20 | `impact falls`, `impact` (header: `a FRIEND's blow`), `impact direction`, `wall slots ON`, `wall pulled back` | falls played by the game ≈ decided, backups few and ~0.2 s; rear down well above front (+0.50 pressure); in contact > 1 m ahead back near 1-3 %; the time back after an impact ~3-4 s | backups still most of the falls; rear ≈ front; `wall pulled back` many spells `still out`; men walking backwards out of the fight |
| 12 | **D31 - THE AI KICKS AND BASHES, THE KICKED MAN BRACES** (step 65): a 50 v 50 shield battle (watch the front for kicks / bashes); then a tournament or a fight as a looter-tier character kicking a fresh heavy shieldman | 20 | `ai kicks`, `ai kicks by reason`, `ai kicks - ends`, `ai kicks - bashes per swing and the wait for reach` (step 66), `kick brace` (the 4 lines), `impact messages` | since step 66: ~0.6-1 bash a shieldman's swing, kicks ~0.3-1 a man-minute of contact (≈ 80-200 asked in a ~50 man-minute battle), PLAYED ≈ asked, waits mostly kicked, landed a good share; your kicks on a fresh heavy man braced ~85 %, a short stagger now and then, never a fall | `PLAYED 0` (the engine ignores the injected input - tell Claude); men standing still with nothing played; a fresh heavy man thrown about by kicks |
| 11 | **D30 - THE WALL'S BLOCKS COUNT, THE CHARGE HITS AT SPEED** (step 64): your ~20 men in a shield wall + a reserve against ~50 charging looters (your 12:47 battle again) | 15 | `shield front arc`, `AI guard by f WITH a shield`, `sprint - AI` (ends), `sprint into contact`, `impact charges` | the game's WRONG side mostly turned right (front arc); looters reel and fall off the shields; charges well above 14 of 160, `sprinting at the blow` above 0; ends `his first attack` many, `an enemy within 3 m` few | `turned` 0 (the angle read is wrong); looters still slowing before the wall; `sprinting at the blow 0` and `carried` low |
| 13 | **D32 - MORALE EYES AND THE FEAR FLOCK** (step 67): green troops in a thin hold line vs veterans in one; a long grind (watch the blobs form and drift, ALT for the blue morale); the fear flock OFF for a minute mid-battle, then ON | 25 | `fear flock shape`, `fear flock drift`, `fear flock cover`, `fear flock by discipline`, `fear flock spells`, `fear flock vs courage and walls`, `hud: ALT bars` (morale clause), `hud: ALT markers - morale line` | ON: spread below OFF, depth ÷ width above it; drift back small under a hold (≤ 1.5 m a minute); green units afraid well before veterans; anchors and sprints seen; the blue bars falling with the fight | men frozen in place with enemies at their shields (the zero-vector stand), a unit sliding back without end, men walking backwards through their own mates |
| 14 | **D33 - THE WALL PUSHES AS ONE, KICKS THROUGH THE IMPACT MODEL, MEN TIRE MORE** (step 68): your shield wall ordered to ADVANCE (F1, F4) into an enemy line, ~100 v 100; a minute with *Shield walls: advance as one line* OFF, then ON | 25 | `wall slots ON by order`, `wall advance as one`, `wall pulled back` (its step-68 clause), `ai kicks` (whom they landed on, a friend in the way), `impact kicks / bashes landed vs decided`, `Athletics settings at the end` (cost per blow 8.0, right 2.00 / wrong 5.00) | the ADVANCE in-contact leak near the standing one (a few %), not 15%; the wall moving forward as one line and pushing; `unaccounted 0`, decided ≈ landed − on a FRIEND; the lines tiring visibly sooner after the first clash | men standing still in an advancing wall that never closes on the enemy (the step too small), the wall stretching anyway (the frame not honoured), `unaccounted` above 0, `on a FRIEND` still about two thirds of the landed |
| 15 | **D34 - WINNERS DON'T HANG BACK** (step 69): your 100 v 100 again, a battle you WIN with green / mixed infantry (charge once the lines meet); then a losing one (looters into your wall) | 20 | `fear flock and winning`, `idle men`, `fear flock by discipline`, `fear flock spells` (sprints by chance) | the losing band's fear before = after, the clear band's after 0; your tired winners go in, no knot standing back; `idle men` OURS small (the standing ones ranged or the game's); sprints by chance far below 163 | the idle line's OURS large with the fear flock / courage after a win (tell Claude the line), the losing band changed |
| 16 | **D35 - THE FLOCK IS A WIDE BAND, NO LONE RUNNER BEHIND THE LINE** (step 70): your ~30 men in a shield line against ~50 looters (your 15:59 battle again); stay on your horse right behind your line | 15 | `fear flock band`, `lone runners`, `fear flock shape`, `sprint into contact` | the looters stand as a WIDE, shallow mass along your line, 4-6 deep (`fear flock band … ON` men per file ~4-6, width near your line's), a few step in to swing and go back; no looter alone behind your line - `lone runners` small, the fixes' walks / sprints ended counted | a deep column again (men per file above 8, width 2-3 m), lone runners standing behind your line - tell Claude the two lines |
| 17 | **D36 - MEN TIRE FASTER STILL** (step 71): any 50 v 50 or 100 v 100 melee (the same kind as D33's) | 5 | `Athletics settings at the end` (cost per blow 10.0 / hero 7.5 / party leader 5.6; a kick or bash 6.00; shield right side 3.00 / wrong side 6.00; the parry 3.00), `[config] migrated config.json` (once, format 14 → 15) | lines visibly orange / red sooner after the first clash than in D33, the AI's swings thinning out; a recruit empty after about 5 swings | the old 8.0 / 2.00 / 5.00 still in the line (your config.json kept a hand-set value - the migration line says which); men empty so fast that the fight stalls (tell Claude) |
| 18 | **D37 - NO ACCIDENTAL CHARGE ORDERS** (step 72): any field battle with an enemy in view; the orders menu (hold the order key), your infantry selected | 5 | `[orders] first accidental charge turned into a MOVE`, `[orders] first deliberate charge KEPT`, `[summary] order clicks (step 72)` | a drag across / onto an enemy banner places your men (they walk / keep their sprint); a plain click ON the banner icon charges it; F1 F3 still charges what you look at | a drag still charging (`charges SLIPPED THROUGH` above 0, or `turned into moves` 0 after your drags) - tell Claude the line; a click right on the icon that does not charge (raise *Charge click: on the banner within* - Advanced page) |
| 19 | **D38 - LINE DISCIPLINE BY TIER** (step 73): Toni's battle again - your ~60 tier-5 men against ~120 tier-1 recruits in a field battle; then a unit of your own recruits in the orders menu | 10 | `[lines] first clamp of an AI line`, `[lines] first capped arrangement`, `[lines] first time the game read a capped maximum width`, `[summary] line discipline` (3 lines + your orders) | the recruits come as a block about 5 deep (24 wide), never a 2-deep front wider than yours; dragging your own recruits wider, the line stops growing at their limit and a line on screen says so | the recruits still a thin wide front (`clamps applied` 0 on the other side - tell Claude the line); the drag still growing (`PREVIEW … installed but never read at a cap` after a wide drag - the order then narrowed after it: tell Claude) |

| 20 | **D39 - THE GREEN BLOCK STAYS A BLOCK INTO CONTACT** (step 74): Toni's 20:30 battle again - ~180 tier-1 recruits charging your shorter line, ideally by a rock / on a slope; then the same on open ground | 10 | `[block] order / behaviour change of the enemy's Infantry`, `[block] FIRST CONTACT`, `[summary] block shape`, `[summary] block hold … the other side` | the recruits arrive as a ~6-deep block about 30 wide and hit your line as one; no trail round your flank; the [block] lines name what the AI did a few seconds before contact | a wide crowd again / a trail round the flank (`block hold … ON after contact` p90 well above 1.0 x the half-width - tell Claude the lines); the block stops short of your line (a stall) |
| 21 | **D40 - A STANDING WALL STANDS FIRM** (step 75): your shield wall on STOP (F1, then F2 - or moved to a spot and arrived) against an enemy push; mid-battle the switch OFF, then ON again | 10 | `[wall] stand firm (step 75): ON …`, `[wall] first man STANDING FIRM`, `[wall] first lever refused … the step out (leash)`, `[summary] wall stands firm` | ON: nobody steps forward to strike - the front swings, blocks and bashes from the line, a man a kick carries out backs straight in; OFF: the old 1 m steps out and back; the summary's ON in-contact "more than 0.5 m" far below OFF's, strikes a man-minute ON not far below OFF's | men frozen and not swinging at enemies in reach (strikes ON ≪ OFF); the wall drifting / bulging ON; jerky back-and-forth from the walk back (raise *Stand firm: walked back beyond*) |
| 22 | **D41 - SPEARMEN KEEP THEIR SPEARS, THE SECOND RANK THRUSTS PAST** (step 76): your spearmen (spear + sword + shield - e.g. Vlandian / Sturgian spearmen) in a shield wall on STOP against ~40 looters; then the switch OFF and ON | 15 | `[spears] mission start …`, `[spears] first back-rank man`, `[spears] first THRUST past a friend`, `[summary] spears`, `[summary] thrusts past friends` | the front rank holds spears until the looters press in, then swords; the second rank keeps spears and thrusts over the front man; looters fall to second-rank thrusts; OFF: swords at once | everyone still on swords ON (the favors not honoured - try *Spears: force the spear while nothing is close*); second-rank spears still stopping on the front man (`… then an enemy hit … 0 of N`); men flinching as their mates thrust through them |
| 23 | **D42 - PRESS, DON'T PASS** (step 77): Toni's battle again - ~100 recruits against your shield wall (~60 veterans) on STOP; then the switch OFF mid-battle and ON again | 15 | `[press] mission start …`, `[press] first enemy LINE`, `[press] first man PRESSING`, `[press] first man CROSSED … ON / OFF`, `[summary] press, don't pass` (two lines) | ON: the recruits pile up against your wall's front, nobody walks round its ends or in behind it; men come through only where your men fell; OFF: the dance round the ends again; the crossings ON ≈ 0 (not allowed), OFF many "round an end" | recruits frozen at the wall and not fighting; men still circling the ends ON (the clamp not honoured); men stuck behind the wall never walked back; a gap read where there is none |
| 24 | **D43 - THE BATTLE'S RHYTHM: SWAPS AND LULLS** (step 78): two infantry lines of ~60 each grinding (your shield wall on STOP against the enemy's infantry); watch the front; then two charging lines that both tire (both on F1 F3 charge) | 20 | `[rhythm] first SWAP asked / BESIDE / DONE`, `[rhythm] first LULL`, `[rhythm] first lull ENDED`, `[summary] battle rhythm` (two lines) | the tired front men step back one by one as a fresh man comes up beside each - the front never thins; when both fronts are spent a gap of ~3-4 m opens for 10-30 s, the men face each other, then one side comes on again | swaps that leave a hole in the front; men walking out of the wall; a lull with no gap (the native charge pushing through the clamp); a lull that never ends; men turning their backs |
| 25 | **D43b - THE LULL THAT SHOWS, NO WRAPPING** (step 78b): Toni's 225 v 229 again - your ~225 against ~229 recruits on battle_terrain_b, a long grind; watch between the fronts and both your ends | 20 | `[rhythm] first LULL … - the breather clock`, `[rhythm] first lull GAP OPENED`, `[press] first WING man held at an end`, `[summary] battle rhythm (step 78b) - THE LULL'S TRIGGER AND GAP`, `[summary] press, don't pass (step 78b) - NO WRAPPING` | every 45-90 s of grinding both fronts back off until 3-4 m of empty ground shows between them for 10-20 s, facing each other, then come on again; the recruits' wide wings stand off your ends (curled toward them), never round them | no gap in a lull (the gap measured ~0 / never held); a lull ended at once by a charge; men still round your ends; a wing frozen far out doing nothing for minutes |
| 27 | **D44 - WAR CRIES** (step 79, sound only): a field battle of a few hundred a side - give F1 F3 to your infantry, watch the enemy charge, then the long grind | 10 | `[warcry] mission start`, `[warcry] first CHARGE ROAR`, `[warcry] first WAR CRY (charge order / back in / contagion answer)`, `[summary] war cries (step 79)` | on your charge part of the unit roars, ragged over a second or two; the enemy's charge roars too; now and then a man stepping back into the grind shouts and a mate answers | silence; a wall of sound every few seconds; the same man shouting again and again; cries from fleeing men |
| 26 | **D43c - EACH MAN RESTS ON HIS OWN** (step 78c - the lull of D43 / D43b is gone): Toni's 225 v 229 again (a long grind, both charging), then your shield wall on STOP against a fresh enemy until your men tire | 20 | `[rhythm] first REST`, `[rhythm] first resting man WALKED BACK`, `[rhythm] first man BACK IN`, `[rhythm] first man IN BY CONTAGION`, `[rhythm] first REST IN A WALL SLOT`, `[summary] rest (step 78c)` (two lines) | worn-out men step back a couple of metres one by one, facing the enemy, stand and guard, then go back in - never a whole unit at once, never a long walk away, never a back turned; when both sides are spent a gap of a few metres shows by itself; your wall's men stay in their places | a whole unit walking off; men 5 m+ behind their front (`rests more than 5 m behind it` high); `TURNED AWAY` above 0; `WALL men … more than 0.5 m out of the slot` above 0; men resting forever (lasted mean over ~60 s); resting men dragged in by the charge (no gap ever) |

Then play normally (parts E-G). Your gut on each family - too many / about right / too few - is the most useful thing to send.

**D16. The impact spike - does the game follow each lever? (step 47, IMPACT I1, ~25 min - a DIAGNOSTIC, do it FIRST in the morning:
steps 48-53 were built the same night on its assumptions).** The impact system (docs/IMPACT.md - staggers, dropped shields, knockdowns
by both men's Athletics) pulls six engine levers no mod of ours has pulled before. The spike pulls each one with a LOUD fixed value and
measures what the engine really did, each against its own untouched control in the same battle. It is a TEST TOOL, not gameplay: men
will fall over and drop shields for no good reason while it is on.
1. **Switch it on.** Escape → Mod Options (MCM) → Trax Combat Enhancements - Advanced → *Advanced* (the last group) → **Impact test (spike) - not gameplay** ON.
   (With the impact system's own switches from step 48 on, leave them as they are - the spike runs on its own.) The log's next battle
   starts with `[impact] mission start: SPIKE ON (ImpactSpike - a test tool, not gameplay): … the game's stun cap (StunPeriodMax) 0.80 →
   2.00 s`.
2. **A tournament (or the arena), ~5 min** - one man in front of you, many kicks. KICK (the game's Kick key - Options → Controls) and shield-BASH at least **8 times** - every 4 kicks run one of each kind: knocked down (untouched), a plain reel
   (untouched), knocked down and getting up at half speed, a reel at half speed. Get kicked yourself if you can. Block his blows with
   your shield and parry - and every ~10 s your first block makes YOU stagger (the forced stagger lands on whoever blocks first, you
   included - how does it look in first person? IMPACT §8 question 5), every ~20 s a block knocks the blocker down (our own blow).
   What you should see: a kicked man falling (half of them), getting up slowly (a quarter), a kicked man with a shield DROPPING it on
   the ground.
3. **A custom battle 50 v 50 infantry with shields, ~10 min** - charge (F1 F3), let the lines grind; hundreds of blocks feed lever 2 (the
   block stun x3 for half the men) and levers 4 / 6 (a stagger every 10 s, a knockdown every 20 s somewhere in the melee). Kick a few
   men yourself here too.
4. **Read the summary** at the end of each battle (`trax_combat.log`, `[summary] impact spike` - one header and six lever lines; each
   ends `→ WORKS`, `→ FAILS` or `→ UNSEEN - <what to do>`). Then switch the spike OFF (a test tool: never leave it on to play).
- **The lines**: `[impact] spike lever 1: first KnockDown answered at … - kick #1 of the battle, by … on …`, `… first Fall seen after a
  KnockDown kick … 0.05 s after the kick, on channel 0 (Fall, action …)` (or `… showed NO Fall within 1.0 s …`), `spike lever 3: first
  reaction slowed …`, `… first slowed reel measured … lasted 0.85 s at speed 0.5 (untouched reels so far: 0.42 s (n 1))`, `spike lever 4:
  forced stagger #1 … with the engine's priority: ACCEPTED (action …)` (or `refused by the engine`), `… first forced stagger played out …
  lasted 0.9 s`, `spike lever 5: first shield drop queued …`, `… first shield left the hand …`, `… picked up again … 34 s after`, `spike
  lever 6: first blow of our own … a Fall must show`, `… first Fall seen after our own blow …`. The summary: `impact spike (step 47, …):
  on 100% of the battle; levers disabled by an error: none; engine calls N …; cost … ms a tick`, then `impact spike lever N - <what> : <the
  numbers> → VERDICT - <what it means>`.
- **What each verdict means for steps 48-53** (the line itself says it too - IMPACT §7's fallbacks):

  | Lever | WORKS | FAILS - the fallback to try |
  |---|---|---|
  | 1 KnockDown on a kick (the decorator's decision; `Fall seen` of `asked`) | I3 / I5 knock men down through `DecideAgentKnockedDownByBlow` - as built | the engine ignores KnockDown on a kick: I3 knocks down with our own blow after the kick (lever 6); else Harmony on `CreateMeleeBlow` - your call |
  | 2 Block stun x3 (`CalculateDefendedBlowStunMultipliers`; the attacker's recoil and the blocker's guard, treated vs control) | I4's longer block stun and the riposte (the attacker's recoil) - as built | the stun does not lengthen what the engine plays: I4's longer block stun and the riposte use a forced stagger (lever 4) instead |
  | 3 Reaction speed 0.5 (`SetCurrentActionSpeed`; the reel and the fall-and-rise, slowed vs untouched - x2 expected) | I3's longer reel and slower get-up - as built | IMPACT §7 fallback 1: `SetOverridenStrikeAndDeathAction` (a per-man swap of the reaction the engine plays) |
  | 4 Forced stagger (`SetActionChannel act_stagger_backward`; accepted / seen / its length / blocks from inside it) | I4's rolled stagger = `act_stagger_*` on channel 0 - as built; the length line is its natural length; `blocks made from inside it` should be ~0 | I4's stagger falls back to the longer block stun (lever 2) - no animation of ours |
  | 5 Shield drop (`DropItem` queued; dropped / picked up after how long) | I3 / I4 drop shields that way - as built; "picked up again after" tells whether re-pickup is too quick (`ShieldPickupLockSeconds` exists for it) | the queued drop does not happen: DropItem from our tick, else `RemoveEquippedWeapon` (the shield gone, not on the ground) - your call |
  | 6 Our own knockdown blow (`RegisterBlow`, KnockDown, 1 damage; `Fall seen`) | I4 knocks a blocker down with it - as built | IMPACT §7 fallback 2: a crush-through (`DecideCrushedThrough`) knocks a blocker down instead |

  **UNSEEN** = not enough of that event in the battle (the line says what to do: kick more, block more, blocks more than 10 / 20 s apart)
  - play a little more and read it again. A lever `disabled by an error` reads FAILS: send Claude the `[error] impact.…` line with its
  stack.
- **Read first**: lever 1's `Fall seen … of asked` and `the kicks NOT asked … fell anyway 0` (vanilla never knocks down with a kick - a
  fall there is not ours); lever 3's `x2.0` (control → treated); lever 2's `off the main thread N` (expected > 0: the engine calls it on
  worker threads - nothing to fix, just the proof) and its `x` on the recoil and the guard; lever 4's `accepted` by priority mode; the
  header's `cost` (a few µs a tick).
- **Also look (your eye)**: does a forced stagger on YOU look awful in first person (the camera), or fine? Does a slowed get-up look like
  a tired man or like slow motion? Does a man on the ground get hit a lot (the AI using the window - step 53's question)?

**Broken — tell Claude** (D16): no `[impact] mission start: SPIKE ON` line with the switch on; `the decorator was NEVER asked` in lever 1
(another damage model on top of ours); a horse or a rider playing a stagger (must never happen - the log counts `never asked: a rider`);
a crash right after a stagger or our blow (note the time - the log's last `[impact]` line); `[error] impact.…` lines; the game's stun
cap left at 2 s after the battle (blocks in the NEXT battle with the spike OFF feel long - the `restored` count in lever 2).

**D17. The impact model - what WOULD happen in your battles (step 48, IMPACT I2, ~15 min - read it with D16, before the effects of
steps 50-52).** The model decides every kick / bash, block and clean hit (docs/IMPACT.md §3) and writes it down. In step 48 itself NOTHING
changed in the battle; from step 50 on the same decisions are applied, family by family (kicks / bashes since step 50 - D19; blocks 51 -
D20; clean hits + momentum 52 - D21; the board moved by one when step 49 was inserted). `ImpactShadowMode` ships OFF
(you wanted it live in the morning - DESIGN decision 40): to see a battle's numbers with the effects OFF, tick MCM → *Impact:
staggers, shield drops, knockdowns* → **Shadow mode: decide and log only**.
1. **The spike OFF** (D16 is a separate battle). The log's start: `[impact] mission start: the model (step 48 - IMPACT I2, docs/IMPACT.md)
   LIVE - …` (or `SHADOW (ImpactShadowMode) …`) followed by your anchors in words (`a kick on an empty even man: shield 90%, down 80%; a
   block on the right side: stagger 80% / down 40% …; two even fresh men: 0% always …`).
2. **A tournament round** - kick a FRESH opponent a few times (nothing: x below 0 against an even fresh man), then tire him (make him block
   and swing) and kick again; let him tire YOU and block his blows.
3. **A custom battle 100 v 100 infantry with shields, ~8 min** - the lines grind, men tire, blocks by the thousand.
4. **Read the summary** (`[summary] impact …`): the header (decisions by kind, YOU took / dealt, not decided by reason, how many landed blows
   the decorator's seam decided), one line per event kind BY THE DEFENDER'S BAR BAND (100-75 / 75-50 / 50-25 / 25-0 %): how many, the mean
   pressure x, and per outcome `would knock down / drop a shield / stagger a / b / c / d = N (rolled …, mean chance …)`; the riposte by the
   ATTACKER's bar; `impact limits` (refused - on the ground, the immunities, no shield, a rider; halved decisions; riders knocked off);
   `impact lengths asked` (stagger s, the kicked man's reel x, the get-up x, the blocker's stun x, a riposted attacker's recoil s); `impact
   cost` (µs a decision).
- **What to read first**: (a) the 100-75 band is ~0 everywhere (fresh men are never staggered - the model's promise); (b) the 25-0 band of
  `wrong-side blocks` and `kicks` - is the "would" count the battle you want (Anton's anchors 90 / 80 there)? (c) `right-side blocks … would
  stagger` in the 50-25 band - a FEW, not every block; (d) `impact limits … diminished` - if most decisions are halved, men are hit too
  often for the 8 s window; (e) `cost` a few µs.
- **Tell Claude**: the summary lines of one tournament and one battle, and your gut for each family: too many / about right / too few. The
  knobs are all in the new MCM group (the ceilings first; `Mismatch weight` and `Even match band` for how much the bigger man counts).
- The first decision of each kind is in the log in full: `[impact] first kick decided at … s - <attacker>'s kick on <him> (decided and
  logged only - its effect comes in step 51 | LIVE): <him> bar 20% (T 0.73); points 200 vs 10 (bodies …); impact 1.20 (a kick) vs guard 1.00 (no
  guard); skill 250 vs 60 (x1.17 / x0.85) → R 7.37 (…) → x 1.33: knocked down 80% → rolled 0.41 YES; shield dropped 90% → rolled 0.95 no;
  gets up x1.73 slower` - and `[impact] first time <outcome> WOULD happen …` once per outcome. Every decision: `VerboseLogging` on (bucket
  `impact`, rate-limited).

**Broken — tell Claude** (D17): no `[impact] mission start: the model …` line; `impact (step 48 - the model): OFF all mission` with the
switches on; `the engine read failed N` above 0 (an `[error] impact.read` line with its stack); the 100-75 band showing knockdowns or
dropped shields between even men; `not tracked` near the total (the model sees no men); a stutter in a big battle (`cost` above ~10 µs).

**D18. Courage and cohesion - men keep to their group (step 49, ~30 min).** Your exact scenario. Defaults on (MCM → *Courage and cohesion
(AI)*). The log's start: `[courage] mission start: courage: each AI man on foot in a formation weighs his side against the enemy within
6.0 m … a looter (armour 8, level 5) 1.35, an imperial legionary (armour 110, level 26) 5.97 = 4.4 looters …`.
1. **A 20-man shield wall vs ~40 looters** (custom battle: your best armoured infantry, 20 men, F1 + F2 shield wall, halt; the enemy ~40
   looters). Watch 2-3 minutes. **See** (step 49b): the FIRST CLASH HAPPENS - the looters charge into the wall in their fever; the ones your
   men hit fall back; when the fever is spent they stand as a THICK ARC in front of the wall, and SINGLE men surge in now and then, swing and
   come back (`[courage] first surge this battle: …`); your wall men step out 1 m to meet them (step 42); looters who drift round a flank
   alone or in twos turn and walk back to their mass
   FACING you (a backpedal), some sprinting; nobody loops behind the wall alone; no lone looter stands among four of your men. **Lines**:
   `[courage] first walk back this battle: <looter> … odds 0.0x < required 1.00 (base … x tired … x morale … x shock … x fever … x hero …);
   … walking to the nearest bigger group of his side …` and `[courage] first walk back ended … arrived | among his mates …`.
2. **Mid-battle, courage OFF** (MCM → *Courage and cohesion* off, ~1 minute, then on again): `[courage] CourageEnabled switched OFF
   mid-mission at … s: N timid men let go at once`. **See** OFF: the looters wander round the flanks again. The summary compares both halves.
3. **The same wall vs ~200 looters** (if your PC carries it). **See**: the front still holds back, but at the wall's ENDS the crowd wraps
   round - as a mass, not one by one - and the looters' centre drifts round with it.
4. **Anton's addition - shock troops on a tired flank**: let a line of your infantry fight a while (bars yellow / orange - hold ALT), then
   send a FRESH enemy unit with two-handers (e.g. Sturgian / Vlandian two-handed troops, or a custom battle: their heavy infantry with great
   axes) into its flank. **See**: the hit flank men PANIC - they sprint back into the mass (all their sprint), the flank folds in and the
   whole block is pushed as one, ready to be surrounded. **Lines**: `[courage] first PANIC this battle: <man> … shocked (N hits in 6.0 s),
   odds … below 50% of the required …; sprints … s, keeping only 0% of his store`.
5. **The [summary] lines** (send them): `courage isolated (fewer than 2 mates within 6.0 m while an enemy is within it): ON x man-seconds
   per AI man-minute …, OFF y …` - ON well below OFF; `courage distance from the formation's centre …: ON mean … m, p90 … m; OFF mean …, p90
   …` - ON's p90 below OFF's; `courage timid: N times by M of K men looked (… walked back at once, … held …); caused by SHOCK …, by the odds
   alone …; wobbles W` - W a small share of N; `courage regroup: walks back ended arrived …, among his mates …, gave up after 15 s …; time to
   regroup mean … s; sprinted back …`; `courage flocking and panic: walks to the nearest bigger group … , to the formation's centre …; PANIC
   walks …, panic sprints …; compression - … shocked a → b m` (b below a = the shocked pack in); `courage by local odds, in contact …` (the
   low bands mostly timid, ≥ 2 brave) and `… men OUT of their formation …`; `courage toughness … min 1.3x … max …`; `courage cost`; step 49b:
   `courage fever and surges (step 49b): fever presses overriding timidity N looks …; surges started N (x per AI man-minute ON …); STANDOFF -
   timid man-seconds with an enemy 3-8 m and no attack by either in 10 s: S (y per AI man-minute ON)` - surges a few per man-minute, the
   STANDOFF low (high = the lines froze face to face - tell Claude).
**Broken — tell Claude** (D18): the looters never reach the wall (no clash at all) or the STANDOFF rate is high; no `[courage] mission start` line; timid men that stand still far from their group (`gave up after 15 s`
near the walks' total = the engine ignores the walk vector); timid men walking INTO your wall under a charge order (try `Timid men stop
hunting` off - tell Claude which looked better); the isolated rate ON ≈ OFF; wobbles near the timid count (men flickering); `errors` in
`behaviour weights`; a stutter (`courage cost` above ~0.5 ms a tick).

**D19. Kicks that break a tired man - IMPACT I3 LIVE (step 50, ~20 min).** Defaults (MCM → *Impact: staggers, shield drops,
knockdowns*: Impact on, Kicks on, Shadow mode OFF; the spike OFF). The log's start: `[impact] mission start: the model (step 48 - IMPACT I2,
docs/IMPACT.md) LIVE - kicks / bashes, blocks, clean hits, the riposte applied; …` (step 51 made blocks live too - D20; step 52 clean hits
and momentum - D21).
1. **A tournament** (a town's arena, any round with a shield fighter). **Fresh** opponent: kick him 3-4 times - he only stumbles back, as
   before. **Line**: `[impact] YOU: your kick on <him> (his bar 9x%, x -0.xx): no knockdown (0%), shield kept (0%) - reels x1.00`.
2. **Tire him**: make him block and swing (hold your shield up, let him hit it, feint) until his bar is orange / red (hold ALT - his
   Athletics bar above him). Kick him. **See**: his SHIELD falls to the ground (the next frame, not mid-kick) and stays there; he GOES DOWN
   and gets up visibly SLOWLY (up to x2); a kick that does not floor him makes him reel longer (up to x2.5) - your free swing. **Lines**:
   `[impact] YOU: your kick on <him> (his bar 1x%, x 1.xx): knocked down (80%), shield dropped (90%) - gets up x1.9x slower`;
   `[impact] live: first shield drop made from the tick at … s - <him>'s shield in slot N, 0.02 s after the kick …`; `[impact] live: first
   knockdown seen at … s - <him> fell 0.0x s after the kick (channel 0, Fall) - his fall-and-rise slowed x1.9x`; `[impact] live: first
   fall-and-rise slowed at … s - … at speed 0.5x …`; `[impact] live: first get-up measured at … s - <him> was down N.NN s (asked x1.9x); no new
   knockdown on him for 5.0 s`; `[impact] live: first shield left the hand at …`.
3. **Kick him again right after he is up** (within 5 s): he does not go down (`… no knockdown (…% - knockdown immunity)`). Later kicks on
   him are halved for 8 s.
4. **A fresh KING** (a later round, or a lord in the arena - Athletics 200+): kick him fresh - nothing at all (`x` below 0); tire him to red
   and kick - now he falls. **You cannot kick-win against a fresh king.**
5. **Let him tire YOU** (stand and block a strong opponent until you are red) - his kicks can now floor you / take your shield:
   `[impact] YOU: <him>'s kick on you (your bar …%): knocked down (…%) …`. Your dropped shield: walk over it and press the use key.
6. **A custom battle 50 v 50 shield infantry, ~5 min** (the AI kicks and bashes too). **The [summary] lines** (send them): `impact live
   (step 50 - kicks and bashes; LIVE …): N live kicks / bashes by the kicked man's bar 100-75% / 75-50% / 50-25% / 25-0% - knocked down
   decided a / b / c / d = N, applied … = N, Fall seen M (not applied: …); shield dropped decided …, applied …, left the hand M …; YOU: dealt
   …, took …; outcomes counted as courage's SHOCK …`; `impact live lengths: the reel asked x… (max x…) → measured … s (max … s) vs untouched
   reels … s = x… measured; the get-up asked x… → the fall-and-rise measured … s …`; `impact live shields: drops queued from the tick …, left
   the hand …, picked up again … after … s, still on the ground …`; `impact live limits: knockdown refused - on the ground …, within the
   immunity after getting up …; halved by a recent outcome …; levers disabled none; tick … ms`. And the old `impact kicks by the kicked man's
   bar …` line now reads `did knock down` (blocks too since step 51; clean hits still `would`).
- **What to read first**: (a) `Fall seen` ≈ `applied` for the knockdowns (the engine plays our KnockDown flag on a kick - D16's lever 1);
  (b) `left the hand` ≈ `drops queued`; (c) the reel's `= x… measured` close to the asked mean (the slow holds on the engine's own reaction -
  D16's lever 3); the get-up measured ≈ 1.5 s × the asked factor; (d) the 100-75 band shows 0 knocked down / 0 dropped.
- **Your gut**: too many / about right / too few knockdowns and dropped shields? The knobs: `KickKnockdownMaxPercent` 80,
  `KickShieldDropMaxPercent` 90, `GetUpMaxSlowdown` 2, `KickReelTiredExtraPercent` 150; `ShieldPickupLockSeconds` (0) if the AI picks shields
  up again too soon (AI only - never you); `CostPerKickOrBash` (3) if kick-spam is a problem.
**Broken — tell Claude** (D19): `knocked down … applied N, Fall seen 0` (the engine ignores the flag on kicks - Claude's fallback is our own
knockdown blow); `no fall seen after a knockdown` near the applied count; `left the hand 0` with drops queued; the reel measured ≈ the
untouched reels (the slow does not hold - the fallback is SetOverridenStrikeAndDeathAction); `levers disabled` anything but none (an
`[error] impact.live-…` line with its stack above); a man stuck on the ground or frozen mid-reel; a fresh man knocked down by an even man.

**D20. Blocks that break a tired guard, and the riposte - IMPACT I4 LIVE (step 51, ~25 min - read it with D16's levers 2, 4 and 6).**
Defaults (MCM → *Impact: staggers, shield drops, knockdowns*: Impact on, Blocks on, Riposte on, Shadow mode OFF; the spike OFF). The
log's start adds: `[impact] live: blocks live at … s - the game's stun cap (StunPeriodMax) 0.80 → 2.00 s while they are
(ImpactStunCapSeconds); a block's stun x (1 + 1.00 × T) from the blocker's tiredness, every other stun keeps the game's cap`.
1. **A tournament, a shield opponent. Fresh**: swing at his shield 4-5 times - his guard holds as before, nothing happens (no `[impact] YOU:`
   line - nothing to tell).
2. **Tire him** (hold ALT - his Athletics bar above him - until it is orange / red: make him block a lot, kick once in a while). Now swing
   at the side he guards (a RIGHT-side block) and then at the side he does NOT guard while his shield is up the wrong way (a WRONG-side block -
   the log names it). **See**: after some of his blocks he STAGGERS back (a real stagger animation, longer the more tired he is, 1-2 s when
   empty) - your free swing; on the wrong side it comes more often and his SHIELD can fly out of his hand (on the ground the next frame); now
   and then he goes DOWN behind his shield and gets up slowly. **Lines**: `[impact] live: first block stagger - <him> after his right-side
   block - act_stagger_backward from the tick at … s (0.0x s after the block), with the engine's priority, speed 0.xx for 1.xx s asked (the
   animation's own length 0.90 s - a guess until one is measured)`; `[impact] live: first block stagger seen at …`; `[impact] live: first
   block stagger measured at … s - <him>'s lasted 1.xx s (asked 1.xx s at speed 0.xx); no new stagger on him for 2.0 s`; `[impact] live:
   first shield drop after a WRONG-side block made from the tick at …`; `[impact] live: first BLOCKER knocked down - our own blow …`;
   `[impact] live: first blocker's Fall seen at …`. **Watch him inside the stagger**: does he raise his shield before it ends? (the summary
   counts `BLOCKS MADE FROM INSIDE A STAGGER` - ~0 = the window is real).
3. **Now be the tired one**: stand and block a strong opponent until YOU are red. **See**: your guard sticks a little longer after each
   block; some of your blocks stagger you (how does it look in first person? tell Claude); a wrong-side block can take your shield. **Lines**:
   `[impact] YOU: your right-side block of <him>'s <weapon> (your bar 1x%, x 1.xx): you are staggered (7x%) - 1.xx s`; `… your shield
   dropped (…%)`; `… you are knocked down (…%) - gets up x1.9x slower`.
4. **The riposte**: let a FRESH veteran (a lord, a later round's champion) block YOU while you are tired (swing at his shield with an orange
   / red bar). **See**: after some of his blocks YOU reel back (up to ~1 s) - his window. **Line**: `[impact] YOU: <him> blocked you
   (right-side block): you reel (riposte, 4x%) - 0.8x s (x_r 1.xx)`; `[impact] live: first RIPOSTE - the attacker <you> staggered after
   <him>'s right-side block …`. Fresh against fresh: never. And YOU fresh against a tired attacker: `YOU: your parry of <him>'s …: he reels
   (your riposte, …%)`.
5. **A custom battle 100 v 100 shield infantry, ~8 min** (the lines grind, men tire; hold ALT now and then). **See**: as the lines tire,
   men in the front rank stagger after blocks, shields on the ground between the lines, now and then a man goes down - and fresh troops sent
   in break a tired line faster.
6. **The [summary] lines** (send them): `impact live right-side shield blocks / parries / WRONG-side shield blocks by the blocker's bar
   100-75% / 75-50% / 50-25% / 25-0% (step 51; LIVE …): N live (a / b / c / d); staggered decided … = N, applied … = N, seen M; [shield
   dropped …;] knocked down (our own blow) decided …, applied …, Fall seen …`; `impact live block staggers: asked … s → measured … s at speed
   x…; the engine took them with its priority …, over it …, not seen …; BLOCKS MADE FROM INSIDE A STAGGER N (should be ~0 …), hits taken
   inside one M`; `impact live blockers knocked down: our own blows registered … → Fall seen …; the get-up asked x… → the fall-and-rise
   measured … s; shields dropped on a wrong-side block: …`; `impact live riposte (a fresh blocker staggers the ATTACKER) by the attacker's bar
   …: decided … applied … seen …; his stagger asked … s → measured … s`; `impact live block stun: the decorator's calls N (off the main thread
   K), lengthened L (x… mean: … → … s, at the cap …), a fresh blocker's left as the game's …; the guard measured: asked x… → … s vs fresh
   blockers … s = x… measured; the game's cap 0.80 → 2.00 s (raised …, restored …)`; `impact live block limits: …`.
- **What to read first**: (a) the 100-75 band: 0 staggered / dropped / down on every block kind (fresh men's guards are vanilla); (b) WRONG
  side above right side in every band, and both near the ceilings in 25-0 (90 / 50 / 40 vs 80 / 40) - **exhaustion never cancels**: two
  empty men's blocks still break; (c) `seen` ≈ `applied` for the staggers (the engine plays our act_stagger mid-fight - D16's lever 4) and
  the measured length ≈ the asked; (d) `BLOCKS MADE FROM INSIDE A STAGGER` ~0; (e) `off the main thread` above 0 (the seam's threading -
  RESEARCH §R2) and `the guard measured … = x…` above 1 (the longer stun reaches the engine - D16's lever 2); (f) `Fall seen` ≈ our blows
  (lever 6).
- **Your gut**: too many / about right / too few staggers on a tired man's block? The knobs: `RightBlockStaggerMaxPercent` 80,
  `ParryStaggerMaxPercent` 80, `WrongBlockStaggerMaxPercent` 90 / `WrongBlockShieldDropMaxPercent` 50, the knockdowns 40, `StaggerBaseSeconds`
  0.6 / `StaggerTiredExtraPercent` 150, `BlockStunTiredExtraPercent` 100, `RiposteStaggerMaxPercent` 50; `ImpactBlocksEnabled` off keeps only
  the riposte.
**Broken — tell Claude** (D20): `seen 0` with stagger `applied` above 0 (the engine refuses our stagger mid-fight - the fallback is the longer
block stun alone); `not seen` near the applied count; the engine taking them only `over it` (fine - say so) or `refused` (`the engine refused
it` in not applied); `BLOCKS MADE FROM INSIDE A STAGGER` near the staggers seen (the stagger is no window - the AI blocks through it); a
stagger that looks wrong (a man sliding, a rider - must never happen: riders are never staggered); `the game's cap 0.80 → 2.00 s` and blocks
feeling long in the NEXT battle with blocks off (the cap must come back - `restored`); `levers disabled` anything (an `[error] impact.live-…`
line with its stack above); our blows registered with `Fall seen 0` (lever 6 does not floor a blocker in game).

**D21. The SHOCK CHARGE - clean hits, the reeling bonus and MOMENTUM - IMPACT I5 LIVE (step 52, ~30 min - Anton's own scenario).**
Defaults (MCM → *Impact: staggers, shield drops, knockdowns*: Impact on, Clean hits on, Tired men always flinch on, Momentum on, +60% at a
full sprint, no bonus up to 2.0 m/s, full from 7.0 m/s; Shadow mode OFF; the spike OFF). The model's mission-start line now reads `LIVE -
kicks / bashes, blocks, clean hits, the riposte applied; …; tired men always flinch on; momentum (the attacker's run into the blow) +60% impact
at 7.0 m/s and faster, none at 2.0 m/s or slower; a rider's horse +30% at 10.0 m/s, none at 3.0 m/s (a couched lance never)` (step 53).
1. **A tournament round (sword or blunt), you against one man. Fresh**: hit him clean 4-5 times - he flinches as in vanilla, nothing more (no
   `[impact] YOU:` line for a hit that does nothing).
2. **Tire him** (ALT: his bar orange / red) and hit him clean. **See**: his flinch lasts LONGER (up to ~1.5 s empty) - a window for your next
   swing; now and then a clean hit KNOCKS HIM DOWN and he gets up slowly; he never shrugs off even a light hit any more. **Lines**: `[impact]
   live: first clean hit that STAGGERS at … s - <you>'s <cut | pierce | blunt | two-handed> blow on <him> (his bar N%, x …): his own flinch to be
   slowed to 1.xx s (x… the flinch's 0.45 s - a guess until one is measured)`; `[impact] live: first clean-hit flinch seen at …` / `… slowed at
   …`; `[impact] live: first clean-hit stagger measured at … s - <him>'s flinch lasted 1.xx s (asked 1.xx s, x…); he counts as REELING until
   then - the next blow on him gets the +0.75 …`; `[impact] live: first shrug-off REFUSED at … s - <him> (bar N%, T 0.xx) took N damage, the
   game's threshold N x (1 - T) = N - a tired man flinches`; `[impact] YOU: your blow on <him>: he is knocked down (N%; his bar N%, x …) - gets
   up xN slower`.
3. **The reeling bonus**: stagger a man (a kick on a tired man, a wrong-side block, a clean hit) and hit or KICK him again while he still reels.
   **See**: a kick on a reeling man very likely puts him down, even when he is not that tired; a heavy blow can too. **Lines**: the first
   knockdown line says `REELING +0.75`; the summary's `on men REELING from an impact stagger N of M hits (…%)` far above `on the rest`.
4. **ANTON'S SHOCK CHARGE - a custom battle**: your side = a shield-infantry line (F1) + a FRESH two-hander unit held back (F2: Sturgian
   heavy axemen / Vlandian voulgiers / Battanian falxmen - any two-handed or polearm infantry), the enemy ~150 infantry. Let F1 and the enemy
   grind for 4-6 minutes until both lines are orange / red (ALT shows the bars). Meanwhile walk F2 round to the enemy's FLANK or behind them
   (F2 + Move - step 37: a move keeps 80% of their sprint). Then F2 + F3 CHARGE at 10-20 m. **See**: they sprint in (the sprint), and where they
   hit the tired line men are thrown around - staggered, knocked down, shields flying; the men who reel go down to the next blow; the line
   breaks from behind (courage's shock - step 49 - pulls the survivors back to their mates). Do it again with the charge against the enemy's
   FRESH reserve - it should bounce (momentum alone hardly staggers a fresh man). And once with **Momentum** OFF (MCM, mid-battle - live) for the
   comparison.
5. **The [summary] lines** (send them): `impact momentum (step 52 - every decided kick, bash, block and clean hit by the attacker's speed into
   the blow; …): standing N (0.0 m/s, impact x1.00): an outcome N%, down N%; on men below half a bar N: an outcome N%, down N% | walking … |
   running … | sprinting N (8.x m/s, impact x1.6x): an outcome N%, down N%; on men below half a bar … || RIDERS (step 53 - …): standing … |
   galloping … | couched lances (no momentum - the game's own big hit) N` (the riders' part: D22);
   `impact live clean hits, <cut | pierce | blunt | two-handed>, by the hit man's bar 100-75% / 75-50% / 50-25% / 25-0% (step 52; LIVE …): N
   live (a / b / c / d); knocked down decided … = N, applied … = N[, Fall seen (all classes) M]; staggered (his flinch lengthened) decided …,
   applied …`; `impact live clean-hit knockdowns: on men REELING from an impact stagger N of M hits (…%), on the rest … (…%); Fall seen …, not
   seen …; the get-up asked x… → the fall-and-rise measured … s; knockdowns answered on a blow the game had shrugged off N`; `impact live
   clean-hit staggers (the engine's own flinch slowed): asked … s (x… the flinch) → measured … s vs untouched flinches … s = x… measured; no
   flinch seen …, fell instead …, controls …`; `impact live shrug-offs (ImpactTiredNeverShrugsOff - the game's threshold x f): the game let a
   tired man shrug a clean hit off N times by his bar … → REFUSED (he flinched) … = N (…%), kept …`; `impact live clean-hit limits: …`.
- **What to read first**: (a) THE PROOF - the momentum line: `sprinting` down % and outcome % far above `standing`, most of all `on men below
  half a bar` (the smoke: a 40% line hit once - a standing swordsman 6% down, a sprinting two-handed axe 36%; an exhausted line 54%); (b) the
  clean hits' 100-75 band ~0 everywhere (a fresh man's flinch is vanilla), the 25-0 band near 80 / 50 - exhaustion never cancels; (c) `Fall
  seen` ≈ the knockdowns applied (the engine plays our KnockDown on a weapon blow); (d) the staggers' `measured` ≈ `asked` and above the
  untouched flinches (`= x… measured` above 1 - SetCurrentActionSpeed holds on the flinch, D16's lever 3); (e) `REFUSED` well above 0 in the
  50-0 bands (tired men flinch); (f) knockdowns on REELING men far above the rest.
- **Your gut**: does the charge feel devastating - or too much / too little? The knobs: `ImpactMomentumPercent` 60 (the charge's punch),
  `ImpactMomentumSprintMetresPerSecond` 7 (lower = a run already counts fully), `HitKnockdownMaxPercent` 50 / `HitStaggerMaxPercent` 80 (clean
  hits), `ImpactStaggeredBonus` 0.75 (how easy a reeling man is to drop), `ImpactTiredNeverShrugsOff`.
**Broken — tell Claude** (D21): `Fall seen 0` with clean-hit knockdowns applied (the engine ignores KnockDown on a weapon blow - the fallback
is our own blow from the tick, like a block's); `knockdowns answered on a blow the game had shrugged off` with no falls (the ShrugOff flag
cancels the fall); `no flinch seen` near the staggers applied; the staggers' measured ≈ the untouched flinches (the slow does not hold);
`sprinting 0` in a battle where your men clearly sprinted into the enemy (the speed read is wrong - tell Claude the walking / running counts);
a FRESH man knocked down by a clean hit from an even man; `levers disabled` anything (an `[error] impact.live-…` / `impact.shrug-off-hook`
line with its stack above).

**D22. RIDERS - knocked off their horses, the cavalry charge, and your on-screen lines - IMPACT I6 LIVE (step 53, ~20 min).** Defaults
(MCM → *Impact: staggers, shield drops, knockdowns*: Impact on, Shadow mode OFF, *Rider knocked off his horse* 50%, *Momentum: a rider's blow
at a gallop* +30%, no bonus up to 3.0 m/s, full from 10.0 m/s; *On-screen lines* on, *your side's heroes too* off, one every 1.5 s; the spike
OFF).
1. **You on foot against a rider** (a custom battle: you as infantry, the enemy a few cavalry; or a lord on horseback in a field battle). Let
   him come; block, parry, swing at him as he passes - and at his horse's rider when he slows. **See**: a FRESH rider never comes off. Tire
   him (he blocks a lot - his bar orange / red under ALT, riders' bars sit higher) and hit him: now and then he is **knocked off his horse**
   (the game's own fall from the saddle); a kick or a wrong-side block can take his shield; he NEVER plays a stagger (the horse moves with
   him). On screen (purple): `You knock <him> off his horse`. **Lines**: `[impact] live: first rider KNOCKED OFF his horse at … s - <you>'s
   clean hit on <him> (his bar N%, x …, the knockdown's chance x 50% = N%) - answered in the damage decorator's dismount seam
   (DecideAgentDismountedByBlow)`; `[impact] live: first rider SEEN off his horse at … s - <him>, 0.0x s after …`; for a block: `[impact] live:
   first BLOCKING rider knocked off his horse - our own blow (RegisterBlow, CanDismount, 1 damage) on <him> …`.
2. **You on a horse, tired** (ride, swing a lot until your bar is orange / red, then fight infantry): some hits knock YOU off - red on screen
   `You are knocked off your horse`; your shield can go (`Your shield is knocked from your hand`). `[impact] YOU: <him>'s clean hit on you
   (riding) (your bar N%, x …): you are knocked off your horse (N%)`.
3. **THE CAVALRY CHARGE** (a custom battle: your infantry line grinds the enemy's 4-6 min until both are orange / red; a FRESH cavalry unit
   held back (F3) charges the tired line at a full gallop - then once into a fresh line). **See**: where the horses hit the tired men,
   men are thrown down and staggered more than by the same riders standing - but not the whole line; into a fresh line nothing more than
   vanilla. Lancers who couch their lances get no bonus from us (the game's own big hit). **The momentum line's RIDERS part**: `galloping N
   (10.x m/s, impact x1.30): an outcome N%, down N%` well above `standing` and `walking`.
4. **The on-screen lines** (any fight): knocked down / knocked off / shield gone / a riposte on you (red); you knock a man down / off his horse,
   break his guard, knock his shield away (purple). Several at once arrive as ONE line a moment later ("You knock Aldric down; you knock Rolf
   off his horse"). Try *On-screen lines: your side's heroes too* ON in a field battle with companions: a white line when one of your men
   unhorses or fells an enemy LORD, or when a companion of yours goes down. *On-screen lines* OFF: none.
5. **The [summary] lines** (send them): `impact riders (step 53 - a knockdown on a rider = KNOCKED OFF his horse at 50% of its chance, never a
   stagger; LIVE …): live decisions on riders kicks / bashes N / blocks N / clean hits N; dismounts DECIDED N (by the rider's bar …) →
   APPLIED a in the decorator's dismount seam + b by our own blow (blocks) → SEEN off the horse within 2.0 s c + d, still mounted e + f (off
   after 0.xx s on average) (not applied: …)`; `impact riders' shields and limits: shields knocked from a rider's hand decided N … → queued … →
   dropped …; staggers REFUSED on riders (never on a horse) N; ripostes refused on a mounted attacker N; …; YOU: knocked off N, knocked someone
   off N …`; `impact messages (step 53 - on-screen lines, ImpactShowMessages on, only yours, at most one every 1.5 s): offered N (to you …, by
   you …, your men …; knocked down …, knocked off a horse …, shields …, guards broken …, ripostes …) → lines SHOWN N, parts MERGED into a line N
   …`; the momentum line's `|| RIDERS …` part.
- **What to read first**: (a) `SEEN off the horse` ≈ `APPLIED` on both ways (the engine plays a dismount from our answer AND from our own
  CanDismount blow); (b) `staggers REFUSED on riders` above 0 and no rider ever seen staggering; (c) the RIDERS columns: `galloping` outcomes
  above `standing`; couched lances counted apart; (d) `lines SHOWN` a few per minute of your fighting, `MERGED` a small share.
- **Your gut**: riders knocked off too often / about right / too rarely (`ImpactRiderDismountPercent` 50); the cavalry charge's punch
  (`ImpactMomentumRiderPercent` 30, `…RiderGallopMetresPerSecond` 10 - lower = a canter already counts fully); the lines - helpful or noisy
  (`ImpactMessageMinSeconds` 1.5, `ImpactShowMessages`).
**Broken — tell Claude** (D22): `still mounted` ≈ applied (the engine ignores our dismount - the seam's or our blow's: say which way); a
rider or a horse playing a stagger animation (must never happen); a crash right after a rider is knocked off (note the time); `riders'
shields … dropped 0` with drops queued; no `impact messages` line, or lines shown for things that did not happen (shadow mode must show
none); the same event shown twice; `levers disabled` naming *a rider knocked off his horse* (an `[error] impact.live-dismount-blow` /
`impact.model-dismount` / `impact.dismount-hook` line with its stack above).

**D23. The AI PUNISHES a window - a reeling or downed man is pressed (step 54, IMPACT I7, ~10 min).** Your point: "otherwise it's a
player-only weapon" - when a man staggers or goes down, the enemy fighting him should go for it, and that includes YOU. Defaults (MCM →
*Impact: staggers, shield drops, knockdowns*: *The AI punishes a staggered or downed man* ON, *Punish: attack eagerness in an opening* 100%
- the game's own full eagerness, above our 60% fresh / 20% empty and the fever's 80% -, *grace after the opening* 0.5 s, *a timid man steps
in within* 3 m).
1. **A 50 v 50 shield battle** (a custom battle, both sides infantry with shields): charge, let the lines grind 4-6 min until most bars are
   orange / red (ALT). Watch a man whose block breaks (he staggers) or who is kicked down: the man in front of him should swing at once,
   more eagerly than the men around - and a timid man standing back within ~3 m steps in for it.
2. **Half-way, switch it OFF** (Escape → MCM → the setting above) for 2-3 min, then ON again - the summary compares the two rows.
3. **Your own guard**: tire yourself (block a lot), let a fresher enemy break your block or kick you - while you reel, the men on you should
   swing harder than before (no line on screen for that; the log has it).
- **The lines**: `[impact] mission start, the AI punishing a window (step 54): ON - …`; `[impact] first window this battle: <him> reeling /
  down at … s until … s (+ 0.50 s grace) - the AI men fighting him press it (attack decisions x1.00)`; `[impact] first AI man pressing a window
  this battle: <him> at … s - his target <victim> reeling, 1.4 m off: attack decisions x1.00 (his own share x0.35) until the window closes`;
  `[impact] first window punished this battle: <him> started an attack at … s on <victim> (reeling) - the punish ON`; `[impact] first timid man
  stepping into a window this battle: …`; `[impact] YOU: first window of yours punished this battle - <him> attacked you at … s while you were
  reeling`.
- **The [summary] lines** (send them): `impact punish windows: opened N (on AI men a, on you b) - seen by an AI enemy in contact N (on you …),
  PUNISHED (an AI melee attack started inside) N (on you … of b), wasted N (N% punished)`; `impact punish proof: AI melee attacks per
  man-minute in contact (on foot) - punish ON: target reeling / down R (…), target not R (…); punish OFF: …`; `impact punish men: raised to
  the window's share N times, ended N, timid men sent in N; AI attacks inside a window on you N; target reads N in N looks, cost N ms`.
- **What to read first**: (a) the ON row's `target reeling / down` rate above its `target not` rate AND above the OFF row's `target reeling /
  down` - that is the proof the AI uses the window (the OFF row is the AI's own reaction to a staggered man); (b) `PUNISHED` a good share of
  `seen` (a window nobody in contact saw cannot be punished); (c) `on you` - your windows punished.
- **Your gut**: does a tired line now collapse too fast once a few men stagger (lower *Punish: attack eagerness* to 80, or the grace to 0),
  or does it feel right - a broken guard gets punished? Does the AI's pressing look natural?
**Broken — tell Claude** (D23): ON = OFF in the proof line (the native AI does not react to the raised eagerness within a short window - the
fallback is a direct attack wish through step 16's input hook); `seen 0` with windows opened (the target read finds nobody - the AI's
target is not the man it fights); `target reads` far above `looks` / 4 (a cost leak); `[error] fever.poll` lines.

**D24. CAVALRY FLOCKING - tired riders gather with their own kind and go in as waves (step 55, ~15 min; built 2026-10-18, installed after
the 46-54 test run).** Your point: "when they tire they won't still go in to charge one by one ... like a flock of birds ... the flock FLIPS from
flocking up to going after its target as a flock". Defaults (MCM → *Cavalry flocking (AI)*): *Cavalry flocking* ON, a rider charges alone at
**75%** eagerness (his bar) or more; below it he wants a group of his kind: 3 at ~57%, **4 at 40%**, about 9 of a 20-rider kind at 20%, **65%** of
his kind when empty; a flock goes in when it has the size its riders' average wants AND its most rested rider is at least half as eager as a free
one; the wave charges for 10 s; a flocker gives up after 45 s.
1. **A custom battle with MIXED cavalry on both sides** (horse archers, javelin riders and lancers - e.g. Khuzait or Aserai vs Vlandia, ~60
   riders a side + infantry): charge your cavalry (F1 + F3) into the enemy (or let the AI charge). The first passes are the game's - every rider
   on his own. Hold ALT now and then (the Athletics bars above the riders).
2. **As they tire** (bars yellow / orange): riders wheel off and ride TOGETHER - lancers with lancers, horse archers with horse archers - a group
   that keeps moving (not a blob standing still), drifting back toward the unit, then bending toward the enemy and going in as **a group of 3-5**.
   Late in the battle, with everyone orange / red, bigger groups and longer gaps between charges.
3. **Switch it OFF** (Escape → MCM → *Cavalry flocking*) for 2-3 min - the game's trickle of single riders again - then ON. The summary compares.
4. **Your own held cavalry** (F1 + F1 hold / F2 move): untouched - they keep their slots. You on a horse: never flocked.
- **The lines**: `[cavalry] mission start: cavalry flocking: every AI rider under a CHARGE order …; lever: Agent.SetScriptedPosition …`; `[cavalry]
  first rider flocking this battle: <him> (melee riders) at … s - tired: eagerness 62% (bar 62%) below the free line 75%; his flock has N riders of
  his kind; slot N`; `[cavalry] first FLIP this battle at … s: 4 melee riders go in together - 4 flocking within 25 m, their mean eagerness 55%
  wants 3; the leader's eagerness 64% (ready 85%), the goal's share of the pull 70%; a wave for 10.0 s`; `[cavalry] CavalryFlockEnabled switched
  OFF mid-mission at … s: N flocking riders let go at once`.
- **The [summary] lines** (send them): `cavalry flocking: N AI riders seen (melee riders a, horse archers b, skirmishers c) … rider time ON: FREE x%,
  FLOCKING y%, IN A WAVE z%; flocks started … ended: the wave (flipped) …, rested (free again) …, gave up …`; `cavalry flips (a flock released at
  once toward its goal - a wave): N (… a minute) of M riders, max …; wave size by the flock's mean eagerness: <25% … riders, 25-50% …, 50-75% …,
  75%+ …; the leader's eagerness mean …`; `cavalry time between passes per rider …: ON <25% … s, 25-50% … s, … ; OFF …`; `cavalry lone passes (a
  pass with fewer than 3 of his kind within 30 m): ON a of b (x%) … OFF …`; `cavalry spread …`; `cavalry lever: Agent.SetScriptedPosition … - points
  sent N (… re-sent as the flock moved), refused (no ground there) N, released N, errors N; … cost … ms a census`.
- **What to read first**: (a) LONE PASSES ON well below OFF - the proof against "one by one"; (b) the wave sizes growing toward the tired bands
  (small fresh, big tired); (c) the time between passes ON growing as the bands fall; (d) `gave up` a small share of the ends (else the riders
  never rest enough for a leader - tell Claude, the leader threshold becomes a setting); (e) `points sent` ≈ flocks started × a few and
  `errors 0`.
- **Your gut**: does it LOOK like a flock (moving together, not stacked, not standing)? Do the waves feel like real cavalry charges? Too much
  hanging back (lower *Riders charge alone above*, e.g. to 60, or *Group size: half-tired wants* to 3) or still too much trickling (raise them)?
**Broken — tell Claude** (D24): tired riders keep charging alone while `points sent` climbs (the engine does not follow a scripted point on a
charging rider - the fallback is our own steering through the input hook); riders frozen or circling one spot (the point sent is unreachable -
`refused (no ground there)`); a flock that never flips (`gave up` most of the ends); `[error] cavalry.*` lines.

**D25. RIGHT BLOCKS FAVOUR THE DEFENDER (step 56, ~15 min; the spike OFF).** Your point: "make sure right blocks are a possible penalty for the
ATTACKER - now I felt them as a penalty for the defender". Defaults (MCM → *Impact*): a right-side block / parry staggers the BLOCKER at most
**30%** and knocks him down at most **10%** - and his own tiredness counts only **25%** (*Correct block: blocker's own tiredness counts*), so it
takes a big mismatch (a stick against a two-handed axe); the **riposte** - the ATTACKER reels - up to **70%**, by the attacker's tiredness and
how outmatched he is, whoever holds the guard (*Riposte: lowered by the blocker's tiredness* 0); the WRONG side unchanged (90 / 50 / 40); the
block stun's lengthening off (*Blocker's guard held longer* 0 - the spike's lever 2 failed). An old config.json moves once (`migrated config.json
… format 9 → 10`; a value you set yourself is kept and named).
1. **A tournament round, swords and shields**: fight until both of you are orange / red (ALT). Block his swings on the RIGHT side - you should
   (almost) never be staggered or knocked down by a correct block; HE should reel off your guard more and more as he tires (the purple "you
   break X's guard" / the YOU line "X blocked you ... you reel (riposte" when it is you swinging into a good guard). Then block on the WRONG side on
   purpose: staggered, shield knocked away, knocked down - your mistake.
2. **A 30 v 30 infantry battle**: tired lines swinging into raised shields should be the ones reeling; the AI presses those windows (step 54).
- **The lines**: `[impact] mission start: … a block on the right side: stagger 30% / down 10% … the riposte 70% (step 56: a correct block counts
  25% of the blocker's tiredness, the riposte x (1 - 0% of his T))`; per decision (verbose / YOU): `… bar 5% (T 1.00, counted x0.25 on a correct
  block) … x 0.25: … riposte (x_r 1.00) …`.
- **The [summary] lines**: `impact live … blocks …` - per block kind × the blocker's band: right-side blocks / parries in the red band with a few %
  staggered, never many knocked down; the riposte by the ATTACKER's band climbing to the tired bands; wrong-side blocks at the old rates.
- **Your gut**: does skill pay now - the right direction safe, a flail into a good guard punished? Tired duels still not a staring contest?
  Too safe (raise *Correct block: blocker's own tiredness counts*, e.g. 50) or the riposte too much (lower *Riposte: attacker staggered, at most*)?
**Broken - tell Claude** (D25): a correct blocker still staggered often in the red band with the spike OFF; no ripostes on tired attackers
(`riposte` decided but never applied - the forced-stagger lever); the config not migrated.

**D26. ROUTING MEN SPRINT AWAY (step 57, ~5 min - in any battle you win).** Your point: "when they are broken and begin to rout, use their
available sprint for the first get-away". Default *AI sprint when a man routs* ON (MCM → *Sprint*). Break an enemy line (a big battle against
looters or a crushed flank): the moment a man turns to flee he should **burst away** at a sprint for a few seconds (men who charged with
everything flee slower - their store is spent), then run on at his tired pace. A formation you ORDER to retreat does not burst (that is not a
rout). **Lines**: `[sprint] first ROUT sprint this battle: <him> broke and routs at … s (the game's own rout - MissionBehavior.OnAgentFleeing) -
he sprints his WHOLE store … s of … s for the get-away …`; **[summary]**: `sprint on rout (step 57, SprintOnRout on): routs seen N, sprints
started M (whole store avg … s), of them taking over a running order sprint …; refused: empty store …, already sprinting …, switched off …, on a
horse …, yours …` - `routs seen` should match the battle's routed men (riders show up under `on a horse`). **Broken - tell Claude**: `routs seen 0`
in a battle with a rout (the callback not reached); routers never visibly faster (the speed check in `sprint speed check`).

**D27. THE ALT VIEW DECLUTTERED (step 58, ~10 min - a field battle, then a tournament round).** Your point: "this ALT view is too cluttered.
Maybe add those lines as some averages on the group, and only show them on up to 5 soldiers that I'm directly looking at and are really close,
making it work in tournaments too". Defaults: *Bars on every man (the old view)* OFF, *Bars on the men you look at: how many* 5, *how close* 15 m,
*Fever share under the formation markers* ON (MCM → *Hold ALT …*). **In a battle**: hold ALT after a charge order - under each marker, below
`ready 34/40`, an orange `fever 12/40` while the men surge (gone when nobody does; the enemy's too); the per-man bars only on the **five men nearest
the centre of the screen within 15 m** - swing the camera across a line and the five bars follow the centre; a man close but at the screen's edge
gets none while five are nearer the centre; the bars do NOT fade when a man is close. *Bars on every man* ON = step 46's view back at once.
**In a tournament / the arena / a duel**: hold ALT - your opponent's bars over his head (how tired, in a fever), the stands' crowd NONE.
**Lines**: `[hud] ALT bars: first bars drawn at … - N men looked at (yours and allies …, enemy …) …; 5 men marked (the 5 nearest the screen's
centre of M candidates within 15.0 m - AltNearMarksMax) …; skipped: …, beyond the near range …, beyond the cap …; … the first drawn: … , 2° off the
centre | …; source: …; mode near: up to 5 men nearest the screen's centre …`; in the arena the first line's `enemy` should be the opponents only (was
140 with the crowd - step 58 skips men with no valid team) and `not on the field` should count the crowd; the values line `… ready 34/40 (40 men,
f 0.90) fever 12/40 | …, fever share on …`; **[summary]**: `hud: ALT bars - … cost … µs a frame …; mode (step 58): near N frames (men marked a frame:
mean …, max 5 - up to 5 nearest the screen's centre within 15.0 m of the camera at the first), everyone 0 frames` and `hud: ALT markers - fever share
(step 58): shown Nx, the most in a fever 30/40 (75%, …); hidden …`. **Broken - tell Claude**: the five bars not on the men in the middle of the
screen (the angle is measured from the combat camera's forward - with RTS Camera's free camera too); the crowd still marked in the arena (`enemy`
~140 again: the spectators have a valid team after all); no `fever 12/40` on a charging formation (the formation pass counts the men's fevers).

**D28. MORE MEN FALL, AND THE CHARGE MATTERS (step 62, ~30 min - three battles; the spike OFF, the defaults).** Your points (2026-10-19): "more
people falling when troops clash, especially when high-tier troops attack low-tier troops ... the shock troop sprinting when hitting them should
knock them down more ... hits from the BACK ... that first charge sprint + swing must matter - met by a wall of shields and blocked the right way,
the ATTACKER is staggered ... even a chance to fall back ... looters' unorganised charges against my shield wall hitting the ground ... even for the
player". Every new number is on MCM → *Trax Combat Enhancements - Advanced* → *Impact: staggers, shield drops, knockdowns* (nothing on the short page;
*Against you* there still scales what happens to you).
1. **Looters into YOUR shield wall** (a hideout / bandit party fight with a shield infantry formation of yours, hold / shield wall, the looters
   charging in): watch the first contact - many looters bounce off the shields (reel back), about one in three of the sprinting ones lands on his
   back. **Lines**: `[impact] live: first CHARGE THROWN BACK (step 62) at … - (looter) charged into (your man)'s right-side shield block: our own
   blow …`; **[summary]** `impact charges (step 62 - …): N of M decided events (sprinting at the blow …, carried after the sprint …) | into a
   right-side shield block K: the attacker reeled …%, THROWN BACK …% (not charging …: reeled …%) …; thrown back applied … → Fall seen …`.
2. **A heavy unit charging peasant archers' backs** (a field battle: your legionaries / heavy infantry round the flank into an enemy archer
   formation - or let the enemy's do it to yours): the archers caught with their bows out should drop - about four in ten of the fresh ones on the
   first sprinting blow from behind, half the tired ones. **[summary]** `impact direction (step 62 - …): front … | side … | rear N (x2.00: down
   …%, an outcome …%)` - rear's down well above front's; `impact toughness (step 62 - …): … much tougher 3x+ … (x2.10: down …%)` -
   the tougher-attacker bands above `even`; `…; on a man with a ranged weapon in his hand N (down …%)`; the `impact momentum` line's `sprinting`
   band no longer 0.
3. **You sprinting into a high-tier shieldman** (a tournament / an arena round against a heavy shield fighter, or a battle): Left Ctrl and swing
   into his shield while he blocks on the RIGHT side - fresh, then again when your bar is orange. Fresh: you mostly bounce (reel) and now and then
   (about one in eight) land on your back with the red line **"You are thrown back by …'s shield"**; at half a bar about one in three. Walk in and
   you only reel; parried, never thrown. **Lines**: `[impact] YOU: you charged into …'s shield: THROWN BACK (…%) - gets up x… slower (x_r …,
   his charge x2.00)`; `[impact] on screen at … (#D65252FF): You are thrown back by …'s shield`.
4. **The falls that were lost** (in any of the three): **[summary]** `impact falls (step 62 - every knockdown decided on a landed blow and what
   became of it; the target: seen = decided): decided N (kicks / bashes …, clean hits …) → played by the game G (…), by our backup blow B (…),
   STILL NOT SEEN S, the man left the field / died first L, its watch taken by a newer reaction R; …; the game's shrug-off REFUSED for a decided
   fall F / stagger St …; landed blows not decided because they KILLED K` - **G + B ≈ N** (the target; on 2026-10-19 it was 7 of 35), S near 0,
   F about the old `knockdowns answered on a blow the game had shrugged off` (14) and that old count now ~0. And `damage critical (step 62 - …): N
   charging melee hits, avg x1.0 (max x1.5); the upside their tiredness would have allowed …%` - a tired man's charge rolls the whole range.
**Broken - tell Claude**: `by our backup blow` ≈ `decided` and `played by the game` ~0 (the engine ignores our KnockDown even unshrugged - then a
KnockBack flag with it is the next lever); `STILL NOT SEEN` high (the backup refused - `refused (down / a siege engine / water)`); `thrown back
applied` but `Fall seen 0` (our blow on a charger is not played); `sprinting at the blow 0` and `carried after the sprint 0` although the units
clearly sprinted (the sprint store is not running on the AI's charge - check `[sprint]`); the looters never thrown back but often `reeled` (raise
`RiposteKnockdownMaxPercent` / `RiposteMomentumPercent`); the archers' `rear` down no higher than `front` (the facing read is wrong - the angle
in the decision line, `from BEHIND (…°, x2.00)`, with VerboseLogging); a shield wall that now breaks under a fresh charge (it should stand -
`impact live right-side shield blocks` staggered ~0 in the 100-75 band).

**D29. FALLS ON THE HIT, A STRONG REAR BLOW, THE WALL PULLS MEN BACK (step 63, ~20 min - one or two battles; the defaults).** Your battle of
2026-10-19 11:02: 198 falls decided, 71 played by the game at the blow, 126 by our backup 0.53 s later - all 126 on a FRIEND's blow (a rear-rank
swing into his own front man: the game cancels that damage and plays nothing, so our backup threw the front man forward, out of his wall); rear
13 % down vs front 11 %; wall men in contact more than 1 m ahead 11.1 %. Now a friend's blow is never weighed, a blow from behind presses like half
an empty bar (*Hit from behind: extra pressure* 0.5, *from the side* 0.2 - Advanced, Impact group; the old ×2 / ×1.4 at ×1), and a wall man thrown
out is held again at once and backs off to his place (*Shield walls: walk a man back after* 0.5 s - Advanced, Battle pace (AI)).
1. **A shield-wall battle** (your infantry in a shield wall, hold, a deep formation - two or three ranks - against an infantry enemy): watch the
   front rank in contact. Men knocked down fall at the blow, not a beat later; nobody tumbles forward out of the wall from behind; a man thrown
   out gets up and backs into his place within a few seconds. **[summary]** `impact (step 48 - …): …; not decided: … a FRIEND's blow (step 63 - the
   game cancels its damage, nothing plays) N` - N in the hundreds in a deep wall (the 126 of 11:02 and more); `impact falls (…): decided D → played by
   the game G (0.00 s after the blow), by our backup blow B (0.2x s after the blow) …; landed blows not decided because they KILLED …, because a FRIEND
   struck them (step 63 …) N` - **G ≈ D, B small**; `wall slots ON: … in contact (an enemy within 5 m) … more than 1 m ahead X%` - **X back near
   1-3 %** (11.1 % at 11:02); `wall pulled back (step 63 - a held man more than 1.25 m ahead of his slot; ShieldWallPullBackSeconds 0.5 s): S spells
   out of place (after an impact I, other O), the farthest … m; ended: back …, freed / let go …, fell or left …, still out at the end …; THE TIME BACK
   - after an impact …s, other …s, the longest …s; walked back by us P times (…) - back with our walk …s, by the game's flag alone …s`; the first-time
   lines `[wall] first man OUT OF PLACE this mission (step 63): …`, `[wall] first man WALKED BACK to his slot (step 63): …`, `[wall] first man back in
   place (step 63): …`.
2. **Into their backs** (send a unit of yours round the enemy's flank into the back of its engaged line - no sprint needed): men struck from behind
   go down far more than face to face, even fresh ones. **[summary]** `impact direction (step 62 - …): front … (x1.00: down …%) | side … (x1.00 +0.20
   pressure: down …%) | rear … (x1.00 +0.50 pressure: down …%)` - **rear's down well above front's** (between even fresh men ~7 % vs 0, archers
   ~1 in 2); the decision line with VerboseLogging: `from BEHIND (…°, +0.50 pressure)`.
**Broken - tell Claude**: `by our backup blow` still most of the falls (then the engine refuses our KnockDown on enemies' blows too - the next lever
is a KnockBack flag with it); the `a FRIEND's blow` count 0 in a deep wall (the enemy check reads wrong); rear ≈ front (the facing read - the angle
in the verbose decision line); `wall pulled back` with many `still out at the end` or `the longest` above 8 s (the walk back does not move him - the
input hook on walled men); men in a held wall walking backwards out of a fight they should hold (the pull fires on men who are not out - send the
`[wall]` first lines); the in-contact > 1 m share still ~10 % with few spells (something else pushes them - send the log).

**D30. THE WALL'S BLOCKS COUNT, THE CHARGE HITS AT SPEED (step 64, ~15 min - your looter battle again; the defaults).** Your battles of
2026-10-19 12:44 / 12:47: ~20 men in a shield wall + a dozen in reserve against ~50 charging looters - looters sprinted early, slowed before the
wall, and nobody fell or bounced off the shields; at the peak a shieldman's blocks read right side 7.3 % / WRONG side 39.0 % (the game decides
"right side" by the guard's direction, and the AI guesses its direction most of the time), only 14 of 160 blows were charges, and the AI's charge
sprints ended `store empty 100, an enemy within 3 m 65`. Now a shield block from the FRONT (within 60° of the shieldman's facing - *A shield facing
the blow blocks it right*, Advanced, Athletics group) is a right-side block - except a tired man's guessed share; the charge sprint runs INTO the
enemy and ends at the first attack (*AI charge sprints run into contact*, Advanced, Sprint); a looter's small sprint waits until the enemy is 10 m
off; the fever sprint starts at 4-8 m; the charge carries 3 s and to the blow of a swing begun in it.
1. **The same battle** (your infantry in a shield wall, hold; the reserve behind; let the looters charge): watch the wall's front. **What you should
   see**: the looters arrive running hard - not slowing a few metres short - and **many reel back off the shields, about one in seven of their
   first blows on the ground** ("thrown back"); later blows still bounce now and then; the wall itself does not move (fresh men). **[summary]**
   `shield front arc (step 64, on, ±60°): AI men's shield blocks N: the game right A (…%) / WRONG B → ours right C (…%) / WRONG D - T wrong-side
   blocks from the front counted right | the game's WRONG side by the attacker's angle off the blocker's facing (turned right): 0-15° … (…), …` - **C
   far above A** (most of B turned), the angle bands mostly under 60° for a wall facing the charge (if many sit at 60-90° the arc could be wider);
   `AI guard by f WITH a shield in hand`: the peak band's `shield right side` now the bigger part; `impact charges (step 62 …): X of Y decided events
   (sprinting at the blow S, carried after the sprint C) - of the carried, by an attack begun in the charge (step 64) K | into a right-side shield
   block …: the attacker reeled …%, THROWN BACK …%` - **X well above 14 of 160**, S above 0, the right-side row's THROWN BACK ~15-35 %; `sprint - AI:
   … ends: …, his first attack (into contact) …, he stopped moving (into contact) …, an enemy within 3 m …` - the first attack the most common end
   of a charge (the 3 m end only for advance / move sprints); `sprint into contact (step 64): … - sprints run into contact R (ended by his first
   attack …, he stopped …); charge sprints that WAITED for the enemy W: started within 10.0 m …` - W ≈ the looters on a charge order; the first-time
   lines `[guard] first shield block from the front counted RIGHT this battle (step 64): …`, `[sprint] first charge sprint WAITING this battle (step
   64): …`, `[sprint] first WAITING charge sprint started …`, `[sprint] first sprint INTO contact ended by an attack …`.
2. **Your reserve's flank charge** (as at 12:47): it still knocks a few down - and now the looters' own charge into the wall bounces too.
3. **Optional - a tired wall** (let your wall grind to orange / red, hold ALT): its guessed blocks stay wrong-side - the `shield front arc` line's `kept
   WRONG by the blocker's tiredness` grows - and the tired wall CAN break.
**Broken - tell Claude**: `T 0` / `turned` empty with many WRONG blocks (the facing read - send the verbose `[guard]` lines); looters still slowing
before the wall with `an enemy within 3 m` the main end of charge sprints (the into-contact mark is not set - send `[sprint]` first lines);
`sprinting at the blow 0` and few charges (the AI starts its attack before contact - then the carry is all there is); men sprinting INTO the wall and
standing there (`he stopped moving` high and the attack never comes - tell Claude: the end at contact may need a time limit); a FRESH wall now
staggered by looters (should be ~0).

**D31. THE AI KICKS AND BASHES, THE KICKED MAN BRACES (step 65, ~20 min - two battles; the defaults).** You: "make the AI do a few shield bashes
and kicks now and then - the game has it but the NPCs never use them" and "make sure a looter kicking a heavy shielded troop doesn't do too much -
maybe now and then a lucky kick can stagger him for a fast bit". Now an AI soldier in reach of the enemy he fights kicks him (no shield) or bashes
him (a shield; a raised shield facing him is kicked) - through his own input (the game's kick key). STEP 66 (your looter battle of 14:16: "ai kicks:
asked 0" in 49 man-minutes of contact; "say 5 pts each"; "maybe make them as frequent as swings when the man has a shield"): a man WITHOUT a shield
kicks about once a minute of contact (a fresh looter 0.7, a legionary 1.2; ×3 against a man behind his guard, ×2 in a charge's fever; 8 s apart);
a SHIELDMAN bashes about as often as he swings while fresh (~0.95 a swing at the top of his bar, ~0.6 at 75-50%, ~0.2 at 50-35%, none below 35%);
a man who decides to kick with the enemy within 5 m waits up to 3 s for him to come in reach; each kick or bash costs 5 Athletics (was 3), you too. And every landed kick / bash asks a BRACE: a
fresh heavy veteran mostly takes it standing (no knock-back, a twitch), a lucky kick staggers him briefly, a spent man cannot brace. Settings:
Advanced, "Impact: staggers, shield drops, knockdowns" - *AI soldiers kick and shield-bash*, *The kicked man braces* and their numbers.
1. **A 50 v 50 shield-infantry battle** (both sides with shields; let the lines meet and grind 3-5 min; watch the front, hold ALT now and then).
   **What you should see** (step 66): the shieldmen in the front ranks shield-bash about as often as they swing while fresh - bash, swing, bash -
   fewer as their bars fall; the men without shields kick now and then (about once a minute each); a fresh shieldman mostly stands it; tired men
   later get pushed around by them, and the bashing lines tire sooner. For a battle like the 14:16 one (~50 man-minutes in contact, ~30
   shieldmen): **roughly 80-200 asked, most of them bashes**. **[summary]** `ai kicks (step 65): ON - …`; `ai kicks:
   asked N (kicks K, bashes B) → PLAYED P (…%), seen … s after the ask → landed on the man L, into a shield S; … asked a man-minute in contact …;
   Athletics spent on them …` - **P close to N** (the engine plays the injected key) and **~2-4 asked a man-minute in contact** (step 66);
   `ai kicks - bashes per swing and the wait for reach (step 66): the shieldmen's melee swings (AI, on foot) N earned … → bashes asked B = 0.6-1.0
   a swing; decisions that waited for reach W → kicked / bashed D (… s after the decision), ran out E, dropped …` - **D well above E** (if most waits
   run out, the men never come within 1.6 m - tell Claude: `AiKickReachMetres` / `AiKickWaitSeconds`); `ai kicks by
   reason …: vs a turtle … | in a fever … | turtle + fever … | plain …`; `ai kicks - ends: played …, NOT played … , interrupted …; why not at a look
   (… looks): cooldown …, too tired …, the dice …` - `NOT played` small. The first-time lines `[impact] first AI kick asked at …`, `[impact] first AI
   kick PLAYED at … - the engine took the injected input`, `[impact] first AI kick LANDED at …`. `kick brace (step 65): N landed kicks / bashes
   decided (… bashes): braced …, short reel …, full reel …, knocked down …; on a SPENT man …` and `kick brace by the kicked man's tier (toughness):
   … heavy (4-7) …: braced …% / short …%` - the heavy fresh men mostly braced; `kick brace in the game: the knock-back seam asked …, the knock-back
   REFUSED for a braced man …; braced reactions seen … (mean … s at x0.20), none shown …; short reels seen … (mean … s at x0.40)`.
2. **You as a looter-tier character** (a weak, lightly armoured character - or a tournament against a heavy shieldman - fresh): kick a FRESH
   heavy shieldman (a legionary / a veteran with a big shield) ten times or so. **What you should see**: he barely moves - no knock-back, his
   shield stays up; **about one kick in eight** a short stagger ("A lucky kick - X staggers for a moment", purple); **never** a fall or a lost
   shield. The log's `[impact] YOU: your kick on X (his bar 100%, x -0.3…): no knockdown (0%), shield kept (0%) - reels x0.20 - BRACED (failure
   13%)` and now and then `... - reels x0.40 - the brace failed (failure 13%)`. Then let him tire you out and tire HIM (fight long, ALT shows his
   bar orange / red) and kick again: the braces fail more and more; below a fifth of his bar no brace at all - he reels long and can fall.
3. **The AI kicks you**: stand behind your shield against an AI man - now and then he kicks your guard open (you reel briefly, or you brace: red
   "You brace against X's kick").
**Too much / too little (step 66 knobs)**: shieldmen bashing so much they hardly swing or the lines tire too fast - `AiBashShareOfSwingsPercent`
50 (one bash in two swings) or `AiBashDelaySeconds` higher; still no kicks - `AiKicksPerMinute` higher, and check the waits line.
**Broken - tell Claude**: `PLAYED 0` with many asked (the engine ignores the injected kick key for an AI agent - send the `[impact] first AI kick`
lines; the fallback is setting the agent's event flag from the tick, as the player's controller does); AI men freezing for a second and a half
in front of an enemy with nothing played (the commit holding them); `played as the other kind` large (bashes come out as kicks or the other way);
a fresh heavy man knocked back by every kick (`the knock-back REFUSED` 0 while braced is high); braced men looking odd (a jerky twitch - the
5 x speed; tell Claude, `KickBraceReelPercent` higher softens it).

**D32. MORALE EYES AND THE FEAR FLOCK (step 67, ~25 min - two battles; the defaults).** You: "give me eyes on their morale too: ALT to see the
average morale of the whole group, and the same 5 closest guys' morale too", and "as troops lose their morale and freshness, they tend to flock
less to their formation position and more to the centre of their unit's mass, but pushed back away from the enemy mass ... stepping behind their
comrades ... this will also make it harder for non-professional men to keep a thin line". Now every AI soldier on foot feels FEAR from low morale
and a spent bar, less the higher his tier (a looter all of it, a legionary 30%), half of it his unit's; above 15% fear his place slides from his
slot toward his unit's SHELTER (its centre, pushed back from the enemy near it, creeping back at most 6 m a minute - 1.5 m under a hold / a
wall), behind a comrade if nobody covers him; a FRESH comrade in front anchors him 20 s; far or alone he sprints back. A shield wall holds until
its men's morale falls below 30. Settings: Advanced, "Courage and cohesion (AI)" - *Fear flock: afraid men bunch up and fall back* and its
numbers; "Hold ALT ..." - *Morale bars above the men*, *Morale and freshness under the near formations*.
1. **Hold ALT in any battle**: look at a soldier - under his Athletics bar (and his fever bar's place) a BLUE bar, his morale (full = 100). The
   formation markers near you (the 4 nearest within 60 m) say "morale 64, fresh 41%" in blue under the fever line. Watch them fall as a side
   loses men. In a tournament: no blue bar (the game keeps no morale there). **[summary]** `hud: ALT bars - … ; morale bars a frame (step 67): mean
   …, max … (their morale … mean)`; `hud: ALT markers - morale line (step 67): pushed N x, … formations a frame on the screen; the lowest morale …
   (enemy …, fresh …%)`.
2. **Green troops in a thin line vs veterans in a thin line** (a custom battle or a field battle with recruits / looters on one side, high-tier
   infantry on the other; order both into a single-rank line - F3 line, wide - and hold). **What you should see**: the green line's ENDS draw in
   toward its middle once the enemy is close (nobody behind them: the thin-line fear) and the line thickens by itself; the veterans keep their
   rank. **[fear]** `first fear-flock spell this battle: … level 6 (discipline 0.00), an enemy … m off, NOBODY behind him → his own fear …,
   the unit's … → fear …, pull … (15-80%); order hold / move; his place … m from him, the shelter … m behind the unit's centre`.
3. **A long grind** (50 v 50 infantry, both sides holding then charging; let it run 5-8 min until the bars are orange / red and morale drops).
   **What you should see**: the tired, shaken units lose their formation shape and become BLOBS around their centre, edging slowly back from the
   enemy; men step in behind comrades; a man alone sprints back to the mass; the enemy can wrap round such a blob. Veteran units keep their
   shape far longer. A shield wall stays a wall until its men's morale (ALT) goes under 30, then it breaks into a blob.
   **[summary]** `fear flock shape …: spread … ON … m, OFF … m; depth ÷ width in contact ON …, OFF …` - **ON tighter / deeper than OFF**;
   `fear flock drift …: hold / wall ON … / OFF …; advance …; charge …` - small and positive (back) ON, never runaway; `fear flock cover …: a comrade
   between him and the nearest enemy ON …% … OFF …%` - ON above OFF; `fear flock by discipline: green … afraid …%; … veteran … afraid …%` - green
   far above veteran; `fear flock spells: N by M of … men looked; ends …; ANCHORED behind a fresh comrade …; stepped in behind a comrade …; sprints
   back …`; `fear flock vs courage and walls: courage's walk back took the man … looks; walls broken … times; … cost … ms a tick; errors 0`.
4. **The fear flock OFF for a minute mid-battle** (MCM → Advanced → Courage and cohesion (AI) → *Fear flock: afraid men bunch up and fall back*
   off, then on again): the blobs stretch back out toward their slots at once; on again, they draw in. `[fear] FearFlockEnabled switched OFF
   mid-mission at … s: N flocking men let go at once`. The OFF minute fills the summary's OFF columns.
**Too much / too little**: units collapsing into blobs too early - `FearStartPercent` up (20-25) or `DisciplineMaxPercent` up; the drift too fast /
too slow - `FearDriftMetresPerMinute`; green lines not thickening - `FearThinLinePercent` up; walls breaking too soon - `FearWallBreakMorale` down.
**Broken - tell Claude**: frightened men FROZEN with an enemy at their shield and never stepping in to strike (the stand-in-place vector - the
`standing` updates far above `walking`); a unit sliding back without end (drift ON far above 1.5 m a minute under a hold); men walking backwards
through their own mates or into walls (the ground check); the blobs flickering between blob and line every second (the near-slot dead zone).

**D33. THE WALL PUSHES AS ONE, KICKS THROUGH THE IMPACT MODEL, MEN TIRE MORE (step 68, ~25 min - one big battle, two short ones; the
defaults).** Your 100 v 100 of 2026-10-19: your shield wall, ordered forward with F1 then F4 (that is ADVANCE), had 15% of its men in contact
more than 1 m ahead of their places (standing, it was 1%); the AI's kicks and bashes landed 92 times, 33 were decided. You: "they need to hold
the wall and push the whole wall, not to break up", and "tire the men more to slow down the combat again a bit after the first impact". Now:
under an advance (or a move, fall back, follow) within 10 m of the enemy's front each held man is sent only 1 m ahead of his place in the line
as it stands, not to the game's place at the enemy's line - the wall walks and pushes as one; a man who kicked or bashed walks straight back
to his place; no AI man kicks through his own mate; a swing costs 8 (was 7), a right-side shield block 2 (was 1), a wrong-side block 5 (was 2).
Settings: Advanced, "Battle pace (AI)" - *Shield walls: advance as one line*, *advance step (m)*, *advance as one within (m)*, *back to the line
after a kick (m)*; the impact group - *AI kicks: not through a friend (m)*.
1. **An advancing shield wall into an enemy line** (a field battle ~100 v 100 with shield infantry on both sides; put your infantry in a
   shield wall - F2 form, shield wall - and order F1, F4: advance). **What you should see**: the wall walks forward as one line, meets the
   enemy and pushes; the front fights, the ranks behind close up; nobody runs out ahead of the wall into the enemy; when the enemy gives
   ground the whole line follows a step at a time. A charge order (F1, F3) still breaks it into a melee. **[wall]** `first man of a moving wall
   sent AS ONE LINE (step 68): … under the order advance; the game's order slot lay … m from his slot in the line (the enemy's front … m away) -
   sent 1.00 m toward it instead`. **[summary]** `wall slots ON by order (step 68 - the in-contact samples …): standing (stop / hold) …: more
   than 1 m ahead …% | ADVANCE (yours or the AI's) …: more than 1 m ahead …%, more than 2 m …% … | moving …` - **the ADVANCE column near the
   standing one (a few %), far below your 15.4%**; `wall advance as one (step 68): ON … - N paced writes on M men (advance …); the game's order
   slot lay … m on average from his slot in the line …` - the order slot several metres ahead in contact (that was the leak).
2. **The same with the advance as one OFF for a minute** (MCM → Advanced → Battle pace (AI) → *Shield walls: advance as one line* off, then on):
   OFF the advancing wall stretches again (men walking on into the enemy); `[wall] advance as one (step 68) changed mid-mission at … s: OFF … - N
   men given the game's own frame back at once`. Compare the two halves by eye (the summary's by-order line holds the whole battle).
3. **Kicks and bashes** (the same battle - watch the front ranks): `[impact] first AI kick / bash HELD for a friend in the way at … (step 68) - …
   one of his own side stands within 0.50 m of the line between them`. **[summary]** `ai kicks: … → landed on the man N (step 68: on his target
   …, on another enemy …, on a FRIEND (the game cancels it - never decided) …, on a horse / a man not tracked …)` - **on a FRIEND small now**;
   `ai kicks - ends: … a friend in the way (step 68) …; … step 68 - a friend on the line to the enemy (within 0.5 m): held …, clear …`;
   `impact kicks / bashes landed vs decided (step 68): landed on a man N (kicks …, bashes …; by you …) → DECIDED D …; not decided: on a FRIEND …,
   … unaccounted 0 (every landed kick / bash on an enemy decided) | into a raised shield … - not decided: the shield stopped it` - **D = N − the
   named ones, unaccounted 0**; `kick brace: … landed kicks / bashes decided` ≈ that D (less any on riders). **[wall]** `first held man out of
   place after his OWN kick / bash or a step-in (step 68): … stands … m ahead of his slot (more than 0.50 m) - walked straight back`;
   `wall pulled back …; step 68 - after his OWN kick / bash or a punish press N (back in … s)` - about a second.
4. **Men tire more** (any battle; MCM → Tiredness: *Cost per swing* shows 8): the lines reach orange a little sooner after the first clash;
   a right-side shield block costs 2 now, a parry 2, a wrong-side block 5. **[athletics]** mission start: `cost per blow 8.0 / hero 6.0 / party
   leader 4.5 points; a kick or shield bash 5.00 …; a blocked blow costs the defender: shield right side 2.00 / wrong side 5.00 / weapon parry 2.00
   points`. Your config.json (if it held the old defaults) was moved once: `[config] … CostPerBlow: 7.0 → 8.0 (format 13 → 14, the old default;
   step 68, Toni …)` and the two block costs; a value you set yourself is kept (`… kept - your own value (format 13 → 14; …)`).
**Too much / too little**: the advancing wall too slow to close or push - *advance step* 1.5-2; it still stretches - *advance step* 0.5-0.7;
it starts pacing too far out - *advance as one within* lower; men still kicking their mates - *not through a friend* 0.7; the battles too slow
now - *Cost per swing* back to 7 (player page).
**Broken - tell Claude**: an advancing wall that stops dead short of the enemy and never closes (the paced frame inside the engine's stop
tolerance), the ADVANCE leak as high as before (the engine does not walk a held man to our frame - the fallback is the integrity position
itself), `unaccounted` above 0 in the kicks line, `pace … errors` above 0.

**D34. WINNERS DON'T HANG BACK (step 69, ~20 min - two battles; the defaults).** Your battles of 2026-10-19 15:5x / 16:0x: you loved the
looters flocking and backing up, but your screenshot showed a few of your men standing 10-20 m off while your charging infantry finished the
enemy (maybe skirmishers throwing / a crossbowman - fine). The log: fear came from morale and tiredness only, never from who was winning - your
green men averaged pull 0.30 (afraid 67%), 163 'by chance' sprints back, 24269 flocking looks on a charge. Now: a man's fear FADES as his side
wins around him (courage's local odds: unchanged at even, gone at 2 : 1; a broken / routing enemy near him calms him more), a winning unit
relaxes together, nobody is pulled back with no enemy within 12 m, and a charge whose men are winning (half or more) is MOPPING UP - nobody held
near the mass. Losing men flock exactly as before. Settings: Advanced, "Courage and cohesion (AI)" - *Fear: winning calms*, *winning from odds*,
*no fear at odds*, *a broken enemy calms*, *Fear flock: only with an enemy within (m)*, *Charging: mop-up when winning*.
1. **Your 100 v 100 again - you win** (a field battle where your infantry, green or mixed, beats the enemy; charge F1 F3 once the lines meet).
   **What you should see**: as the enemy thins and breaks your tired men go in - no knot of your infantry standing back while the rest kill;
   your skirmishers / archers still stand off to throw and shoot (normal). **[summary]** `fear flock and winning (step 69) by his side's local
   odds: losing (odds below 1.0) N looks: win 0.00, fear a → a (UNCHANGED) …; even to clear (1.0-2.0) …: fear a → b; clear (2.0+) …: fear a →
   0.00, afraid …% → 0.0% …; calmed by winning N looks (spells ended …); no enemy within 12 m N looks …; a charge's MOP-UP (its men's win 50%+) N
   looks` - **the losing band's before = after, the clear band's after 0**; `fear flock by discipline: green …` - the green pull well below your
   0.30 in a won battle; `fear flock spells: … sprints back … (… by chance N …)` - far below 163. **[fear]** the first spell line may say
   `calmed by winning … (his odds …) to …`.
2. **THE IDLE LINE - who holds the men standing off** (the same battle): **[summary]** `idle men (step 69 - AI men on foot on a charge / advance,
   an enemy within 25 m and none within 3 m, standing (below 0.5 m/s) more than 5 s; …): OURS N man-s - the fear flock (step 67) … man-s (… men,
   the enemy … m), courage timid (step 49) …, the brace - guard only (step 23) …, the wall hold (step 41) …; the game itself (none of ours) …; a
   ranged weapon in hand / a ranged or skirmisher formation (NORMAL) …`. **[fear]** `first IDLE man held by … (step 69): (name) at … s - his
   unit on charge, the nearest enemy … m (none within 3 m), standing (… m/s) for … s; fear … → … (win …), pull …, flock …; courage …; brace …;
   wall …; his bar …%` - one per holder, the first time. **This line proves the cause**: OURS small = the standing men were the game's or your
   ranged men; the fear flock / courage large = tell Claude with the line.
3. **A losing fight - they still flock** (looters into your shield wall, or your green line against veterans): the looters bunch up and back off
   exactly as you liked - `fear flock and winning … losing … fear a → a`, spells ending `calm again` / the fever, not `winning`.
**Too much / too little**: winners still hang back - *no fear at odds* 150; men now too bold when barely ahead - *winning from odds* 120,
*no fear at odds* 300; the mop-up too early - *mop-up when winning* 70; rear ranks of a losing line standing too still - *only with an enemy
within* 20 (0 = the old pull without a threat).
**Broken - tell Claude**: the losing band's fear changed (before ≠ after), `fear … errors` above 0, `idle men … errors` above 0, the idle line's
`OURS` large with the fear flock or courage after the win.

**D35. THE FLOCK IS A WIDE BAND, NO LONE RUNNER BEHIND THE LINE (step 70, ~15 min - one battle, two looks; the defaults).** Your 30 v 50 looters of
2026-10-19 15:59: the frightened looters stood as a deep, narrow column (~15 men deep - the log's `depth ÷ width in contact 1.44, 3.5 m deep, 2.6 m
wide`, `stepped in behind a comrade 1394 looks`) where you wanted a wide, shallow mass along your whole line, 4-6 deep, a few stepping forward to
swing and back; and right after the first charge a few looters came round BEHIND your line alone - one stood next to you on your horse. The cause
of the column: the flock's shelter was a POINT (a charge pulled its men right onto it), and stepping in behind a comrade stacked the files. Now
the shelter is a BAND along your front: as wide as your line (or their men in ranks), 4 ranks deep a little afraid to 6 terrified, its front 1.5
m off yours pushed back by fear; each man keeps his place along the front and only closes up from behind; men too deep in a file slide out to
its ends; front men within 2.5 m of you are let go to swing and walked back past 2 m. And a man ALONE (fewer than 2 mates near, an enemy within 8
m) out beyond his line (4 m ahead of his unit's front, or past yours) is a LONE RUNNER: his fever no longer sends him in alone, his charge / fever
sprint ends, and he walks back round your line to his group (unless he runs down a beaten enemy). Settings: Advanced, "Courage and cohesion (AI)"
- *Fear flock: a wide band, not a point*, *Fear band: ranks deep …* (two), *file width*, *rank depth*, *at most this wide*, *front stands off the
enemy*, *Fear flock: front men strike within*, *back to the mass beyond*, *Lone runners come back*, *Lone runner: ahead of his line*.
1. **Looters into your wall** (your ~30 men in a shield line or a plain line, ~50 looters; let them charge, then let it grind). **What you should
   see**: after the first clash the looters back off a little into a WIDE mass facing your whole line, 4-6 men deep - no tall column - with a man
   now and then stepping in to swing and going back. **[summary]** `fear flock band (step 70 - a wide band along the enemy's front, 4-6 ranks) -
   in contact, each unit's men per occupied 1.0 m file (the depth in RANKS): ON a men per file (p90 b), the deepest file c, width d m (p90 e), files
   over the band's ranks f% (… samples) | OFF …; places: no band yet (the point) …, in the band as he was …, compressed in depth …, slid out of a
   full file …, a full file, no room in the band …; steps in behind a comrade kept …, refused (his file full - he slides instead) …; a charge's men
   left to their AI inside the band …; front men let go to STRIKE N looks …, walked back past the leash … M` - **a 4-6, d near your line's width,
   f small, N above 0**. The step-67 line `fear flock shape … depth ÷ width in contact ON` well below your 1.44. **[fear]** the first spell line
   may end `; THE BAND (step 70) 20.0 m wide, 5 ranks (20 files), its front … m from him - …`.
2. **Stay on your horse right behind your line** during the first charge. **What you should see**: no looter alone behind your line - one who
   breaks through stops sprinting and walks back round the end of your line to his mates. **[summary]** `lone runners (step 70 - the fixes ON; …):
   fixes ON N man-s (M men), OFF … - by cause: the sprint into contact (step 64) …, the punish step-in (step 54) …, a courage surge (step 49b) …,
   the fever press (step 49b) …, chasing a target behind the enemy's front …, the game itself …; his target behind the enemy's front: you (the
   player) …, a hero …, another man …; …; THE FIXES: the fever's press refused for a man alone …, walks back forced …, surges / punish step-ins
   ended …, sprints into contact ended …` - **N small (a few man-seconds), the forced walks and the sprints ended counted**; the cause columns
   tell which suspect it was (your 15:59 log could not - send this line). **[courage]** `first LONE RUNNER by … (step 70): (name) at … s - … m ahead
   of his unit's front, … m past the enemy's front, alone …; his target you (the player) BEHIND the enemy's front …`; `first LONE RUNNER walked back
   this battle (step 70) …`; **[sprint]** `first sprint INTO contact ended for a lone runner this battle (step 70) …`; `sprint into contact` ends
   `alone or past the enemy's front (step 70) N`.
3. **The A / B** (optional, the same fight again): *Fear flock: a wide band, not a point* OFF = step 67's point (the column back; `fear flock band …
   OFF` side filled); *Lone runners come back* OFF = measured only (the `lone runners` OFF column grows).
**Too much / too little**: the mass too thin and wide - *ranks deep* up (5 / 7); still too deep - check `files over the band's ranks` (high = the
men cannot get out to the ends - tell Claude); too far off your line - *front stands off the enemy* 0.5; front men never swing - *front men strike
within* 3; lone runners walked back too eagerly from a mass's edge - *Lone runner: ahead of his line* 6.
**Broken - tell Claude**: `fear … errors` / `lone runners … errors` above 0; the band's men per file above 8 with the band on; a looter standing
behind your line for many seconds with `lone runners` near 0 (the geometry missed him).

**D36. MEN TIRE FASTER STILL (step 71, ~5 min - watch any big melee; the defaults).** Toni after D33: "they need to tire faster still". Every
paid move costs a step more: a swing or shot **10** (8 before; 7.5 for a hero, 5.6 for a hero who leads his party), a kick or a shield bash **6**
(5), a wrong-side shield block **6** (5), a right-side shield block **3** (2), a weapon parry **3** (2). The refill is unchanged (60 s from empty
to full at rest). A recruit is now empty after 5 swings (7 before), a legionary after 13 (17). Your config.json moves once: the first start
logs `[config] migrated config.json at …: CostPerBlow: 8.0 → 10.0 (format 14 → 15, the old default; step 71, Toni: the men tire faster still - a
swing or shot costs 10)` and one line for each other cost still at its old value; a value you set by hand stays and is said once (`… kept - your
own value …`). **What you should see**: the lines go orange / red sooner after the first clash; a soldier who only blocks / parries now sinks unless
his bar is big (120+). **[summary]** `Athletics settings at the end: … cost per blow 10.0 / hero 7.5 / party leader 5.6 points; a kick or shield bash
6.00 / …; a blocked blow costs the defender: shield right side 3.00 / wrong side 6.00 …`. **Too much**: *Cost per swing* (the player page) back to 8,
or the block costs on the Advanced page, Athletics.

**D37. NO ACCIDENTAL CHARGE ORDERS (step 72, ~5 min - one field battle; the defaults).** Toni 2026-10-19: placing a unit with a click or a
drag, the cursor crossing another unit's floating banner turned the order into a charge at that unit, and his men sprinted off. Now only a
plain CLICK with the cursor ON an enemy banner icon (when you press and when you let go) charges it. In the orders menu, your infantry
selected: (1) **drag** a line for them that starts on open ground and sweeps across an enemy unit's banner, and let go ON the banner -
they **move** to the line (the dragged frontage), no charge, no sprint; (2) **click** on open ground right beside an enemy banner (near,
not on it) - they move there; (3) **click right on** the banner icon - they **charge** that unit (the game's own targeted charge); (4) F1
F3 while looking at an enemy - the game's charge as before. While you hold the button off the icon the enemy marker's "targeted" look goes
out - that is the guard at work. **Log**: `[orders] attached: the order-click guard (step 72) - guarding: the game's targeting handler at
N, this view at M …` (M above N = same frame), `[orders] first time the game's unit targeting was held OFF during a click …`, `[orders]
first accidental charge turned into a MOVE at … a drag across a unit … its troop placer's move went out`, `[orders] first deliberate charge
KEPT at …`. **[summary]** `order clicks (step 72): N clicks in the orders menu - K deliberate charges kept (a click on the banner), C charges
turned into moves (D drags, O clicks off the icon; the placer's move seen after …), P plain moves …, 0 charges SLIPPED THROUGH …`.
**Wrong**: a `FAILURE … a targeted charge went out although the click was being turned into a move` line, or the placer's move "NOT seen"
- tell Claude. **Too strict** (a click on the icon does not charge): *Charge click: on the banner within (px)* up (Advanced page, Orders:
no accidental charges); **off**: *No accidental charge orders*.

**D38. LINE DISCIPLINE BY TIER (step 73, ~10 min - one field battle; the defaults).** Toni 2026-10-20: his 60 tier-5 men against 120
tier-1 recruits - the recruits attacked on a very broad front about 2 deep, wider than his whole army, and engulfed him. Now each man's tier
gives his minimum ranks (T0 7, T1 6, T2 5, T3 4, T4 3, T5 2, T6 1 since step 74 - step 73 shipped one rank less below T6; heroes as T6) and a foot formation's line is at most ceil(men ÷ the mean)
wide. (1) **The same battle**: ~60 of your veterans against ~120 recruits (a custom battle or the campaign). Watch the recruits come: a BLOCK
about 6 deep and ~20 wide (hold ALT, look from above), not a 2-deep front wider than yours; as they lose men the block narrows, staying 6
deep. Your veterans hold at least 2 deep (60 of them at most 30 wide). (2) **Your own recruits**: put ~40-60 tier-1 men in one formation, open the orders menu
and DRAG a long line for them (much wider than 8-10 men) - the drawn line of men should STOP GROWING at their limit while you keep dragging;
let go: they form at the limit, 6 deep, and a line on screen says *"your Infantry: these men hold no thinner than 6 deep - 10 men wide at
most"* (60 recruits). (3) Turn *Line discipline: your units too* off (the player page, How the AI fights) and drag again: any width, no line on screen;
back on: the line closes up at once. **Log**: `[lines] mission start: line discipline by tier (step 73): … - ON for the AI; your own
formations too (the order drag's preview stops at the cap, a line on screen when you ask wider)`, `[lines] first formation counted at … s:
the enemy's Infantry - 120 men (120 T1) → D 6.0 → at most 20 files …`, `[lines] first clamp of an AI line at … s: the enemy's Infantry (the
other side) stood 48 files … → narrowed to … m: 20 files x 6 ranks, 6.0 deep (it was made wider - an order or the AI's behaviour)`, `[lines]
first AUTO-SHRINK at …`, `[lines] first capped arrangement at … s: your Infantry now tells the game its maximum width is …`, `[lines] first
time the game read a capped maximum width … - the PREVIEW IS CAPPED`, `[lines] first line order of yours wider than the men hold at … - AT
THE CAP …`. **[summary]** `line discipline (step 73): ON at the end - the map T0 7.0 / T1 6.0 / …; looks …, cost avg … ms a tick …`;
`line discipline, your side (step 73): N formations counted, K capped - … | clamps applied: by the AI's own widening …, on your orders … |
auto-shrinks …`; `line discipline, the other side (step 73): … the enemy's Infantry: 120 men, D 6.0 → max 20 files … vs asked up to 48
files …`; `line discipline - your orders (step 73): … PREVIEW CAPPED (the drag stops growing at the cap) (… the game asked it for its maximum
width and got the cap N times)`. **Wrong**: the recruits still a thin wide front with `clamps applied` 0 on the other side; `clamps the game
had not taken yet` in the hundreds; the drag still growing while `PREVIEW … installed but never read at a cap` (then the order is narrowed
right after you let go - say so); any `[error]` with `lines.` - tell Claude the line. **Too strict / too loose**: the ranks per tier on the
Advanced page (*Line discipline by tier*); **off**: *Line discipline: your units too* (yours) or *Line discipline by tier* (everyone).

**D39. THE GREEN BLOCK STAYS A BLOCK INTO CONTACT (step 74, ~10 min - one or two field battles; the defaults).** Toni 2026-10-20 20:30: the
recruits came as a 5-deep block, then just before contact spread into a wide loose crowd with a trail wrapping round his right along a rock.
The cause in the game's code: near contact the AI general's tactic switches the infantry from its advance to a TACTICAL CHARGE, and a
formation wider than its enemy then gets a plain CHARGE order - no formation places, every man at his own target. Now a formation of low
line discipline keeps its block anyway. (1) **The same battle**: ~180 tier-1 recruits against your ~100 infantry (a shorter line) + archers,
the same map or any with a rock / slope on a flank. Watch the last 10 s before contact from above: the block keeps its ~6-deep shape and its
width (~30 men, ~44 m), men drifting out sidestep back into it facing you, nobody trails round your flank; it hits your line at full pace.
(2) **Open ground**: the same armies on a flat map (your second battle) - the block as before. (3) Optional A / B: *The block holds into
contact* off (Advanced, *Line discipline by tier*) - the old crowd. **Log**: `[lines] mission start: the block hold (step 74): … - ON`;
`[block] first look at the enemy's Infantry (the other side, AI) at … s (no contact yet): - → Move / BehaviorAdvance / Line - 180 men … m wide
…`; THE CAUSE: `[block] order / behaviour change of the enemy's Infantry … (−N s from its first contact): Move / BehaviorAdvance / … → Charge /
BehaviorTacticalCharge / …` (or a Loose arrangement, or nothing - say which); `[block] FIRST CONTACT of …`; `[block] first man walked back
into his block …`. **[summary]** `block hold (step 74): ON at the end - …`; `block hold (step 74), the other side - … ON before contact mean …
m of … m, p90 … x the half-width …, beyond the allowance …% | ON after contact … ; PULLED IN N times by M men (round the flank …)`; `block
shape, the enemy's Infantry (the other side, AI): first contact at … s | −10..−5 s: Move / BehaviorAdvance / Line 100%; width … | −5..0 s: … |
0..+5 s: Charge / BehaviorTacticalCharge / Line …; width …, out of slots …%`. **Wrong**: a trail round your flank with `PULLED IN` near 0 (the
block not counted - `looks: not held by step 73` high: tell Claude); `ON after contact` p90 far above 1 with many pulls (the engine ignores the
vector in a charge - tell Claude); the block stalling before your line; any `[error]` with `block.`. **Too strict / too loose**: *Block: room
past its width*, *Block: full hold from depth* (Advanced, *Line discipline by tier*).

**D40. A STANDING WALL STANDS FIRM (step 75, ~10 min - one field battle; the defaults).** Toni 2026-10-22: "the men in the shield wall still
step forward even when on a standing order, they take a few steps to strike at an enemy, then fall back." Now a wall on a stop never leaves
its ground. (1) **ON**: your infantry in a shield wall (F3 → shield wall), ordered to **stop** (F1, then F2) - or moved to a spot and let it
arrive - and let ~40-60 enemies (looters, then a real infantry unit) push at it. Watch the front from the side: nobody steps forward to swing;
the front blocks, swings and bashes from the line; a man a kick or a shove carries a little out backs straight in, facing the enemy. (2)
**OFF mid-battle**: Escape → MCM → player page → *Shield walls stand firm on a hold order* off: the old 1 m steps out and back return within
a quarter second. (3) **ON again**, then order the wall to **advance** (F1, F4): it pushes as before (stand firm is only for a hold). (4)
Optional A / B on Advanced, *Battle pace (AI)*: *Stand firm: the game walks each man to his place* and *a tight tolerance for the game* off,
one at a time. **Log**: `[wall] stand firm (step 75): ON (ShieldWallStandFirm) - …`; `[wall] first man STANDING FIRM this mission (step 75): …
under the order Stop (a stop stands at once) …`; `[wall] first lever refused for a man standing firm (step 75) - the step out (leash): …`
(and `- pushed / the game's approach / a blow`, `- his own kick / bash or a punish press`, `- a sprint`); `[wall] first strike by a man standing
firm (step 75): … from his slot`; `[wall] stand firm (step 75) changed mid-mission at … s: OFF …` and back ON. **[summary]** `wall stands firm
(step 75 - …): held N man-s (in contact M man-s) by K men | IN CONTACT ahead of the slot - ON more than 0.5 m …%, more than 1 m …% … | OFF …
| strikes - ON from the slot …, out of it … (… a man-minute in contact); OFF … | levers refused - the step out (leash) …, a sprint …, the
advance pace …, his own kick / bash or a punish press …, pushed / the game's approach / a blow … ; walked back … times, back in … s; the
engine's walk target pinned to the slot … times …`. **Right**: ON "more than 0.5 m" a few % at most and "more than 1 m" ~0 (OFF: the old
leash's ~10-20%); strikes a man-minute ON close to OFF's; walked back in well under a second. **Wrong**: strikes ON far below OFF (men
frozen - the engine's keep flag too strict for a stand-firm wall: tell Claude); ON ≈ OFF ahead (the engine ignores the pinned frame / the
walk back - tell Claude); the wall sliding forward over time; any `[error]` with `wall.firm`.

**D41. SPEARMEN KEEP THEIR SPEARS, THE SECOND RANK THRUSTS PAST (step 76, ~15 min - one or two field battles; the defaults).** Toni
2026-10-22: "my shield wall-ers have spears but they keep using their swords, even though the spear has a longer reach". (1) **ON**: a
custom battle (or a field battle) with your spearmen - men carrying a spear, a sword and a shield - in a **shield wall** (F3) on **stop**
(F1, F2), against ~40 looters. Before contact everyone holds the spear. When the looters press in, the FRONT rank swaps to swords after a
couple of seconds; the SECOND rank keeps its spears and thrusts over the front man's shoulder - watch whether those thrusts reach the
looters (a looter falling to a man behind the wall) or still stop on the front man, and whether a front man FLINCHES when his mate's
spear passes him. When the looters fall back, the front takes its spears up again within ~4 s. (2) **OFF mid-battle**: Escape → MCM →
player page → *Spearmen keep their spears* off: the game's own choice (swords at once, Toni's complaint). (3) **ON again**; optional
A / B on Advanced, *Battle pace (AI)*: *Thrusts pass a friend* off (the second rank's thrusts stop on the front man again), *Spears: the
back ranks keep their spears* off. (4) If the men ignore the preference (swords everywhere ON): *Spears: force the spear while nothing
is close* on (the wield fallback). **Log**: `[spears] mission start, the AI prefers the spear (step 76): ON (SpearPreferenceEnabled) - …`
and `… thrusts past friends (step 76): ON (ThrustsPassFriends) - …`; `[spears] the friendly-hit stun (StunPeriodAttackerFriendlyFire) 0.40 s
→ 0.10 s at … s`; `[spears] first AI man with a polearm AND a sidearm this mission: … close radius … m`; `[spears] first back-rank man this
mission: … - rank 1 of a Wall under Stop, a man of his in front: he keeps the polearm whatever is close (holding the polearm)`; `[spears]
first flip to the sidearm (an enemy close) …` / `… back to the polearm (nothing close) …`; `[spears] first man facing only polearms …`;
`[spears] first THRUST past a friend this mission at … s: X through Y with a polearm, from a back rank - the weapon carries on`; `[spears]
first thrust past a friend that then HIT AN ENEMY …`; `[spears] first SWING into a friend … - it bounces as in the game`; `[spears] first
attack by a back-rank man holding his polearm …`. **[summary]** `spears (step 76 - …): N AI men carry a melee polearm AND a sidearm … |
TIME HOLDING - ON … man-s; nothing close … s: polearm …%, sidearm …%; an enemy close … s: …; a back rank … s: …; facing only polearms …
| OFF …`; `spears - the flips: to the sidearm …, back to the polearm …, back at once in a back rank … | the favors the decorator wrote … |
attacks begun holding the polearm …, the sidearm …; by back-rank men … (with the polearm …, … a man-minute …) | the wield fallback …`;
`thrusts past friends (step 76 - …): friendly melee contacts N - a thrust passed …, a swing (bounced) …, a kick / bash … | passed: with a
polearm …, by back-rank men … | THEN AN ENEMY HIT by the same man within 0.5 s: K of P (…%; landed …, blocked …, no damage …) | the
friend passed: flinched … no reaction … | the friendly-hit stun: 0.10 s (the game's 0.40 s …)`. **Right**: ON, "nothing close" time
mostly the polearm, "a back rank" time ~all the polearm, "an enemy close" mostly the sidearm; OFF far more sidearm in "nothing close";
back-rank attacks with the polearm a few a man-minute; a fair share of passes followed by an enemy hit; the friend's "no reaction" well
above "flinched". **Wrong**: ON ≈ OFF in "nothing close" (the native AI ignores the favors - try the wield fallback, tell Claude);
back-rank attacks ~0 (the native AI never starts a thrust with a mate in front - the next lever is RBM's thrust collision data, AI_NOTES
"Step 76"); passes never followed by an enemy hit (ContinueChecking does not carry the spear on - the same); front men flinching at
every pass; any `[error]` with `spears.` or `thrust.`.

**D42. PRESS, DON'T PASS (step 77, ~15 min - one or two field battles; the defaults).** Toni 2026-10-22: 108 T1 recruits against his 59 T5
shield wall - at contact their rear ranks broke off and walked round his wall's ends and behind it, "dancing", then came in through the
middle; stand firm off changed nothing. (1) **ON**: the same battle - ~100 recruits (or looters) against your shield wall (F3) on **stop**
(F1, F2). Watch the recruits at contact: they should pile up against your FRONT, the back ranks behind their own front rank - nobody walking
round your ends or in behind the wall. Where your front men fall and nobody steps up (a gap of 2 m+), men may come through it; a man who got
behind anyway walks back round the nearest end. (2) **OFF mid-battle**: Escape → MCM → player page → *Press, don't pass* off: the dance
round the ends should come back. (3) **ON again**. (4) Optional: a much WIDER enemy line (60+ men in a long line against a short wall) -
its flanks should wrap round your ends as a body, never one or two men alone. **Log**: `[press] mission start: press, don't pass (step 77):
… - ON`; `[press] first enemy LINE at … s: your Infantry - held by step 41, N men looked at; its front … m wide (… men hold it), … m deep,
no gap`; `[press] first man PRESSING an enemy line at … s: … m in front of your Infantry …`; `[press] first man PUSHED back out …`; `[press]
first man walked ROUND a line's end …`; `[press] first GAP in a line …`; `[press] first man let ROUND a line's end with his mass …`; `[press]
first man CROSSED an enemy line's front with the press ON / OFF at … s: … round an end / through the middle …, cause …, allowed / NOT
allowed`. **[summary]** `press, don't pass (step 77): ON at the end - lines published N (held by step 41 …, a held block …, holding their
slots …, squares / circles …), their fronts … m wide …; GAPS found …; units WIDER than their line …; looks ON: pressing (clamped) …, past
the front - pushed back out …, behind the line - walked round its end …, through a gap …, round the end with his mass … | OFF (measured):
…; CLAMPS applied … input frames …; the fear band clamped to its enemy line … times (+… ranks deeper); the block hold's sideways range cut
… looks; cost …` and `press, don't pass (step 77) - men who CROSSED an enemy line's front …: ON N - round an end … (charge target …, the
game …), through the middle …; allowed: a gap …, a wrap … | OFF …; men past a front at a look …: ON …% | OFF …%`. **Right**: ON the
crossings not allowed ≈ 0 and "men past a front" far below OFF's; OFF many crossings "round an end" (charge target / the game); the
recruits still swinging at the wall (the impact / attack lines as before); the fear flock band's width p90 no wider than your wall + 2 m
and its "slid out of a full file" far below 3730. **Wrong**: men still walking round the ends ON (the engine ignores the clamped vector -
tell Claude); recruits standing off the wall not fighting; a man stuck behind the wall for long (`walked round` count high, crossings
still rising); `[press] first GAP` on an unbroken wall; any `[error]` with `press.`.

**D43. THE BATTLE'S RHYTHM: SWAPS AND LULLS (step 78, ~20 min - two field battles; the defaults).** Toni 2026-10-22: "two packs of dogs - they
don't want to get hurt but they won't back down" - the clash, the grind (the tired front men hold while fresher men come up), then a lull with
a gap, the most tired going to the back. (1) **THE GRIND**: your ~60 infantry in a shield wall on STOP (F1, F2) against ~60 enemy infantry (not
looters - they need a few ranks). After the first clash, watch one file of your wall: a tired front man should stand and guard while the man behind
him steps up BESIDE him, then he backs into the second rank, facing the enemy. The front should never show a hole for it. The enemy's line does it
too. (2) **THE LULL**: a battle where both sides charge (F1 F3) with mid-tier infantry and nobody breaks quickly. When both fronts are worn out
(the ALT bars mostly orange / red at the front) both should ease back ~2 m each, facing each other - a gap of ~3-4 m - for 10-30 s; the tired go
to the back; then one side comes on again (it recovered, or you gave a new charge order - try F1 F3 in a lull: it should end at once). (3) Optional:
Escape → MCM → player page → *Lulls: your units ease back too* off - your men keep at it, and so does the enemy facing them. **Log**: `[rhythm]
mission start: the battle's rhythm (step 78): ROTATION: … - ON; LULL: … - ON`; `[rhythm] first SWAP asked at … s: <tired> (25%) at the front in
contact, <fresh> (85%) right behind him …`; `[rhythm] first fresh man BESIDE the tired one at … s after … s: … the slots swapped
(Formation.SwitchUnitLocations) …`; `[rhythm] first SWAP DONE at … s in … s …`; `[rhythm] first tired front man who could not swap … - nobody
right behind / nobody fresher behind / …`; `[rhythm] first LULL at … s: <unit> (Charge, front bar 22%, …) and <unit> (…) - both fronts spent after
… s in contact; each eases back 2.0 m (the gap between the fronts … m now)`; `[rhythm] first man WALKED BACK in a lull …`; `[rhythm] first WALL SLOT
shifted back in a lull …` (a held wall); `[rhythm] first swap DURING A LULL …`; `[rhythm] first lull ENDED at … s: … - a front recovered / a new
charge order / … after … s, the gap opened by … m`. **[summary]** `battle rhythm (step 78) - ROTATION ON: tired front men looked at ON … | OFF …;
chances …; swaps asked …, done … (slots swapped …), ended: done …, never beside …, refused: …; the bar at the swap: tired …% vs fresh …%; the tired
man held until the fresh one was beside him: mean … s; men rotated …, during lulls …; the bar of men IN CONTACT ON …% | OFF …%; input frames …` and
`battle rhythm (step 78) - LULLS ON: started … (front bars at the start mean …%), lasted mean … s, the gap opened mean … m, ended: …; men walked
back …, wall slots shifted …; both fronts SPENT in contact ON … | OFF …; would have started OFF …; …; cost …`. **Right**: swaps done most of those
asked (the "never beside" few); the bar of men in contact ON higher than OFF (flip *Tired front men swap with fresh ones* off in a second battle
to compare); lulls 1-3 in a long grind, 10-30 s each, the gap opened 2.5-4 m, ended mostly "a front recovered"; the walls' men walking back and
forward again with their slots. **Wrong**: holes in the front during swaps (the fresh man not coming up - "never beside" most of the swaps); men
swapping back and forth every few seconds; a lull with no gap (the gap opened ~0 - the engine ignored the clamp in a charge: tell Claude); a lull
that only ends at 30 s every time (the thresholds); men turning their backs; any `[error]` with `rhythm.`.

**D43b. THE LULL THAT SHOWS, NO WRAPPING (step 78b, ~20 min - Toni's 225 v 229 again; the defaults).** Toni 2026-10-22 16:10: "between the two
fronts [a lull], never, not for a sec" (2 lulls in 435 s, the fronts meshed, one ended at once by a charge order) and the enemy's 69 m of recruits
went round both ends of his ~31 m line - "they should be like packs of dogs ... they want to stay in their pack". (1) **THE BREATHER**: the same
battle - your infantry against ~229 recruits, a long grind, nobody breaking early. Every 45-90 s of continuous fighting (sooner when you hold)
both sides should back off until a clear gap of 3-4 m of empty ground lies between the nearest men all along the contact, facing each other, for
10-20 s - also when the two crowds were meshed - the tired going to the back; then they come on again. Try F1 F3 (charge) right as a lull begins:
it should hold for ~3 s, then end. (2) **NO WRAPPING**: watch both your ends - the recruits' wings should curl toward your line's end and stand off
it (slanting back away from you the farther out they stand), never walk round it or in behind you. Then order one of YOUR units to a spot beside
the enemy's end (a plain move): it must go there. **Log**: `[rhythm] first LULL at … s - the breather clock (after … s in contact, the clock … s):
<unit> (…, the unit's bar …% over … men in the fight, its front men …%) and <unit> (…); the blows in contact … a man-minute (the spell's peak …);
THE GAP from the men: mean -… m (p10 …, p90 …) - each side backs to its line …`; `[rhythm] first lull GAP OPENED at … s: … after … s - the gap
from the men mean … m`; `[rhythm] first lull LINE STEPPED BACK …` (when one side pressed); `[rhythm] first CHARGE ORDER in a lull's first 3 s …
- deferred`; `[rhythm] first lull ENDED at … s: … (the breather clock), … held after … s`; `[press] first WING man held at an end at … s: … m past
an end of … - past the end the front bends back 30 degrees …`; `[press] first WING man walked back from round an end …`. **[summary]** `battle rhythm
(step 78b) - THE LULL'S TRIGGER AND GAP: started by the breather clock …, by both units spent … ; THE GAP measured from the men in lulls (…): mean …
m (p10 …, p90 …, the lowest … m) against 3.5 m wanted; it held … after mean … s (…), never …; lines stepped back …; charge orders … deferred …` and
`press, don't pass (step 78b) - NO WRAPPING: the wrap off (PressWrapAllowed) …; WINGS held at an end (looks) ON … by … men (the farthest … m past
an end), walked back to the bent front …; CROSSED round an end ON …; the old wrap - looks let round the end with his mass 0 …`. **Right**: lulls
4-8 in a 7-minute grind, most by the clock, the gap's mean 3-4 m and "never" ~0, held within ~3-6 s; "round the end with his mass" 0, crossings
round an end ON near 0; the wings' farthest metres ≈ the overlap (Toni's ~19 m). **Wrong**: the gap mean below ~2 m or "never" most lulls (the
native charge pushing through the clamp - tell Claude); lulls only by "both units spent" (the clock's bars too low - tune *Lull clock: a charging
unit worn below*); lulls ending at 30 s every time; men round your ends; your own ordered flank move held back.


**D43c. EACH MAN RESTS ON HIS OWN (step 78c, ~20 min; the defaults).** Toni 2026-10-22 after playing 78b: "this doesnt work at all ... they are at it
the whole time, then they like for some reason all just turn their backs and walk away very far off, then they are at it again ... only per men
rule, if tired - step back and wait to recover". The lull of D43 / D43b is GONE (skip their lull parts; the swaps and no-wrapping stay). (1) **THE
GRIND**: Toni's 225 v 229 again (or any long infantry grind, both charging). Watch the front with ALT: when a man's bar goes orange / red he should
step back 2-4 m - WALKING backwards, still facing the enemy, guard up - and stand there, not swinging unless an enemy comes right up to him; after
20-40 s he goes back in. Men should do it one at a time, at different moments - never a whole unit; sometimes a resting man goes in together with
the man beside him. When both sides are worn out a gap of 2-4 m should show here and there by itself; when only the enemy is spent, your fresh men
should push on into them. (2) **A NEW ORDER**: give your charging unit F1 F3 again while some of its men rest: they must not all rush in at once.
(3) **THE WALL**: your shield wall on STOP against a fresh enemy until your men tire - the tired men must stay in their places (guarding, not
striking forward), never step back out of the wall; the wall moves only as one. (4) Optional: Escape → MCM → player page → *Tired men step back and
rest* off and on mid-battle (every rest ends at once; on again = the next tired men rest). **Log**: `[rhythm] mission start: the rest (step 78c): an
AI man on foot at the fight … - ON`; `[rhythm] first REST at … s: <man> at 31% (his tired mark 38%, Charge) with an enemy 1.2 m off - he backs out of
reach (3 m, at most 4 m) facing the enemy, guard up, and waits for his ready mark 74%`; `[rhythm] first resting man WALKED BACK at …`; `[rhythm] first
REST IN A WALL SLOT at …`; `[rhythm] first man BACK IN after a rest at … - his own ready mark`; `[rhythm] first man IN BY CONTAGION at … - … went in
with <man> 1.8 m away`; warnings that should NOT appear: `first resting man TURNED AWAY`, `first resting WALL man more than 0.5 m out of his slot`,
`first resting man more than 5 m behind his own front`. **[summary]** `rest (step 78c) - EACH MAN ON HIS OWN ON: rests started N (in a wall slot …),
lasted mean … s; went back in: by his own mark …, by contagion … (rolls …), other ends: rotated …; stepped back from his start mean … m, p90 … m,
max … m (limit 4.0 m); the farthest behind his own front … m, rests more than 5.0 m behind it …; men TURNED AWAY while resting 0 of …; WALL men
resting in their slots: … looks, more than 0.5 m out of the slot 0 …; the marks rolled …` and `rest (step 78c) - THE NATURAL GAP between engaged
fronts (a diagnostic only …): your team a gap of 2.0 m+ in …% of … unit looks …, the gap mean … m; the other sides …`. **Right**: many rests
spread over the battle, lasting ~20-40 s; back in mostly by own mark with some contagion; stepped back mean ~2 m, p90 ≤ 4 m; TURNED AWAY 0; WALL out 0;
rests more than 5 m behind the front few; the natural gap shows in some share of the looks when both sides are spent. **Wrong**: men walking away
far, or a whole unit backing at once; turned backs (`TURNED AWAY` > 0 - tell Claude: the engine turned them); resting men pulled straight back in by
the charge (stepped back mean ~0, no gap ever); a wall man stepping out; men resting for minutes (lower the ready marks); no rests at all (the
tired marks too low for your battles).

**D44. WAR CRIES (step 79, ~10 min; the defaults; SOUND ONLY).** Toni 2026-10-22: battles should sound alive. Nothing about the fighting changes -
listen. (1) **YOUR CHARGE**: a field battle of a few hundred a side; hold your infantry, then F1 F3. Part of the unit should ROAR - ragged, spread over
about a second and a half, not one voice and not everyone; vanilla's own few yells come a second or two later on top. Give the same order again
within 15 s: no second roar from us. (2) **THE ENEMY'S CHARGE**: when the AI general sends its men in (in vanilla that is silent) its units should
roar the same way. (3) **THE GRIND**: in the long fight, now and then a man stepping back in after his rest shouts, sometimes a mate beside him
answers a moment later - single voices, not a choir. (4) **A BIG CHARGE**: many units charging at once should sound like a roar, not a flood - if it
crackles, cuts out or drowns everything, lower `WarCryBattlePerSecond`. (5) Optional: Escape → MCM → player page → *War cries* off mid-battle (silence
from us at once). **Log**: `[warcry] mission start: war cries (step 79, sound only …) - ON`; `[warcry] first CHARGE ROAR at … s: formation slot N got a
new charge order - K of its men queued to shout (40% each, at most K a unit with U units roaring at once) over 0-1.5 s; sound only`; `[warcry] first
WAR CRY (charge order) at …`; `[warcry] first WAR CRY (back in) at … - going back in after his rest`; `[warcry] first WAR CRY (contagion answer) at …`;
`[warcry] first cry dropped by the battle's cap / the man's own limit …`. **[summary]** `war cries (step 79) - ON: cries N - back in … of … events …;
contagion answer …; charge order … (riders …) …; DROPPED by the man's own limit …, the unit's cap …, the battle's cap …, gone …, switched off …, the
queue full 0; charge orders … (roared …, the unit's roar cooling …); PEAK cries in any 1 s: the battle ≤ 16 (cap 16), one unit ≤ 8 (cap 8); one man's
shortest gap … s (limit 8 s)`. **Right**: a roar on every new charge (yours and the AI's), the grind's occasional single shouts, the peaks at or under
the caps, the shortest gap 8 s or more, `gone` small (men who fled before their cry came). **Wrong**: silence (the voice did not play - tell Claude);
the queue full above 0; a man's gap under 8 s; cries heard from fleeing men; a choir.
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
     ready 34/50         ← ours (step 24): the men not bracing - plain, yellow, red; only while bracing is on
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
- Switch mid-battle (Escape → Mod Options): *Hold ALT: formation markers, Athletics and fever bars* → *Enemy formations too* off →
  the enemy's numbers go at once, yours stay; on again. *Numbers under the formation markers* off → all
  go; on again. *Orders menu strip* → *Show average health* off → the `HP` goes here too. *Advanced* →
  *Marker numbers: text size / gap under the marker / bar length / bar thickness (px)* move them live
  (bar thickness 0 = no bar) — tell Claude the numbers that look right.
- Log (appendix L7): `[hud] attached: ALT markers (AltMarkerView, movie TraxAltMarkers, prefab installed) - shown while ModEnabled, AthleticsEnabled and ShowAltMarkerStats are on, the game's Hide battle UI and photo mode are off, in a fight (battle, duel, tournament or stealth mode) and while the game shows its formation markers (ALT held or the orders menu open - read live from its own marker layer)`;
  the first ALT: `[hud] ALT markers: layer created at …` then **`[hud] ALT markers: first shown at … (show #1) - technique: the game's own formation markers read live (their points and their widgets' sizes) (layer MissionFormationMarker, movie FormationMarker: 6 markers, 6 marker widgets); screen 1920 x 1080 px, UI scale 1.00; 6 markers (yours 3, allies 0, enemy 3): yours 1 Infantry (41 men) at (960, 402) 60 x 108, 85 m | …; labels (…): yours 1 Infantry at (960, 458) | …; no label: enemy 3 Cavalry - behind the camera`**
  — THE proof of the alignment: each label's centre x = its marker's x, its top y = the marker's y + half its height + 2 at UI scale 1;
  `[hud] ALT markers: values at … (show #1): yours 1 Infantry 72% ± 8 HP 81% ready 40/40 (40 men, f 0.93) | … | enemy 1 Infantry 64% ± 12 HP 95% ready 80/80 (…) - ± is 1.00 std, health on, enemy on, ready count on: yellow at or below 75%, red at or below 50% of the men ready`.
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
  `[config] reverted all 86 settings to their defaults (…): N changed, applied live`.

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
   at 3 (step 18) - too cheap, too dear?; the battle pace (step 21, D5 / D6) - shield wall 30%, other
   infantry 15%, bows 2.0 s, crossbows 2.5 s: right, too much, too little?; the brace floors (step 23, D7) - hold 60,
   advance 40, charge 20, back at +20 ± 5: do lines turtle, or brace too little?; the ready count (step 24) - readable,
   useful, colours at 75% / 50% right, the strip's extra row fine at your resolution?; Athletics in tournaments and the arena (on now)?; which attack-rate
   switch felt right (C7); the balance — recruits empty after 5 swings, one attack in five at empty, a
   run at 0.6 and swings at 85% at empty (step 20b), ~41 s from empty to full strength at rest along the
   refill curve (half the bar in ~25 s): too harsh, too soft, right?
6. Optional: screenshots of the bar and the strip.

---

## Appendix — the log, line by line

### L1. Load, settings, MCM

- Load: `[load]` lines at every game start (A1), incl. `[load] module: <Id>` - which copy runs (dev or
  release); `[compat]` RBM or not; `[compat]` two copies enabled and the other stood down (A6 - only
  when both are on); `[mcm]` registered or not (A1, A4) — step 12: at most ONE `not ready yet` line,
  followed by `registered at retry (attempt N)` or one `never became ready - gave up after 31 attempts …`;
  with MCM's module off but its DLL carried by another mod, one `MCM's module is not enabled - no settings page; config.json only.`
  and nothing more; `[config] defaults: … 86 keys for 86 settings` (any `defaults.json PROBLEM:` under it →
  that setting runs on a fallback — tell Claude); `[config] settings in effect (70, version N):` and
  one line per setting (a changed one ends `(default …)`). An old config.json still holding an old default is
  migrated once: format 3 (step 14's) `[config] migrated config.json at …: MinMoveSpeedMultiplier: 0.7 → 0.6 (format 3 → 4, the old default; step 20b, …)`
  and `AttackAnimationMinPercent: 100 → 85 (format 3 → 4, the old default; step 20b, …)`; format 2 (step 13's)
  `MinMoveSpeedMultiplier: 0.3 → 0.6 (format 2 → 4, the old default; step 14: …)` and the 100 → 85;
  format 1 (before step 13) also `AttackRateAiDecisions: true → false (format 1 → 4, the old default; …)`
  and `PlayerBarOffsetBottom: 54 → 30`; then `rewrote config.json as format 4 …`. A value you set yourself is never moved.
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
- **The overall reduction (step 25)** comes AFTER the roll: the roll lines above show the roll alone
  (before the reduction); the reduction has its own lines:
  - start: `… - read live on every hit; overall damage reduction (DamageReductionPercent) 25% after the roll, rolled or not (an average 100 hit lands 75; falls and objects never)`
    (`0% - off, the game's damage` at 0; `- not while the mod is OFF` with the master switch off);
  - always, the first reduced hit: `[damage] first reduced hit this mission - <hit>: the game's 18 → after the roll 23 → reduced 25% (DamageReductionPercent) → lands 17`;
  - verbose: every roll line and skip line ends `, reduced → 17` when the reduction changed it;
  - summary: `[summary] damage reduction (DamageReductionPercent 25%, after the roll): 812 hits (melee 650, ranged 120, mounts 40, shields 2); damage the game's 21934 → after the roll 22011 → landed 16508 (-25.0% from the roll's, -24.7% from the game's), avg 20.3 per hit (the game's 27.0); not reduced: fall damage 3, objects (doors, siege engines, ships) 5`
    — proves `-25.0% from the roll's` (a few tenths of rounding); `not reduced` lists `fall damage`, `objects …`,
    `0-damage hits`, `reduction 0 (DamageReductionPercent)`, `mod OFF (ModEnabled)`. None at all: `no hit reduced this mission`.

### L4. Athletics and speed

- Start: `[athletics] mission start: ON - pool = the Athletics skill x1.00, at least 50; full strength at 75% of the pool and above; cost per blow 10.0 / hero 7.5 / party leader 5.6 points; a kick or shield bash 3.00 / hero 2.25 / party leader 1.69 points; a blocked blow costs the defender: shield right side 1.00 / wrong side 5.00 / weapon parry 2.00 points (a hero x0.75, a party leader x0.56), misses cost: yes; when empty: attacks at 20%, run x0.30, horses x1.00 (never slowed); damage upside follows Athletics: yes; wounds cap the pool: yes; refill after 3.0 s rest: empty to full in 60 s at a walk or slower (up to 0.40 of top speed), x0.50 at a full run, the same rate all the way (RegenRateNearFullPercent 100) - read live`
  (step 22: the block prices, run x0.30, the even refill);
  `[athletics] party-leader rule: …` (custom battle: `no campaign (custom battle) - the side's general, or every hero of a side without one`);
  `[speed] stat model on top in this mission: ours, over <the game's model> - …` (**`WARNING: … not ours`** → another mod took the slot: tell Claude);
  `[athletics] party leader: …`, `[athletics] YOU: …` (C1), `[athletics] first tick: tracking N fighters`.
- Always: the `[athletics] YOU …` lines (C1, C3); once per battle the `[speed] first exhaustion this mission`,
  `[speed] first exhausted fighter leaves 0 after …` (`they stayed penalized: yes`), `[speed] first exhausted fighter back at full strength: … (x1.00 …)`
  — "did NOT take the asked factors" or "stayed penalized: NO" → tell Claude; with a horse slowed
  (*Horse speed when the rider is empty (x)* below 1.0): `[speed] first horse slowed this mission: …`.
- Verbose: `~[athletics] pool at spawn: …`, `~[athletics] hero: …`, `~[athletics] blow melee (on foot) / (mounted) / ranged / couched/braced …`
  (`- below full strength`, `- EXHAUSTED`), `~[athletics] kick (on foot): <name> - cost 3.0, 50.0 → 47.0 of 50 (f 1.00 → 1.00)`
  / `shield bash` / `kick/bash at its hit` (step 18), `~[athletics] shield block (right side) (on foot): <name> - cost 1.0, 50.0 → 49.0 of 50 (f 1.00 → 1.00)`
  / `shield block (WRONG side)` / `weapon parry` (step 22, bucket `athletics-block` - the DEFENDER's cost), `~[athletics] exhausted: …`, `~[athletics] off empty: …`,
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
  [summary] Athletics blows charged: 412 (melee swings 300, shots/throws 100, couched/braced hits 12, landed-only swings 0, landed-only shots 0) - by riders 60, on foot 352; + kicks/bashes 8 and blocks 260 (not blows - their own lines); Athletics spent 4400 points (kicks/bashes 24, blocks 486 of them)
  [summary] Athletics detection: melee releases seen 300 (mounted 50) | shots seen 100 (+0 extra projectiles of the same shot ignored) | ranged releases seen by the poll 98 | melee hits by fighters 280 (on foot 240, mounted 40): during a counted release 276, outside one 4 [in action: Other(0) 4]
  [summary] Athletics kicks/bashes charged 8 (23.6 points; by riders 0): kicks 3, shield bashes 5, at their hit with no kick or bash seen 0 | seen starting: kicks 3 (channel 1 0, channel 0 3), shield bashes 5 (channel 1 5, channel 0 0); kick/bash hits 6; free (CostPerKickOrBash 0) 0; each charged once, when it starts; never an attack pause
  [summary] Athletics blocks paid by the defender 260 (486.0 points; by riders 6): shield right side 180 (170.0), shield WRONG side 40 (196.0), weapon parries 40 (120.0) | by you 22 (19.5), AI heroes 4 (6.0), other AI 234 (460.5) | free (cost 0) 0, while Athletics was off 0, the same blow seen again (not charged twice) 12, missiles stopped by a shield (free) 90; each blocked blow charged once to the defender; never an attack pause or a step back
  [summary] Athletics free (never charged): couched hits within one blow-length of the last 2, attacks while Athletics was off 0, …
  [summary] Athletics exhaustions (empty, f 0): 45 entered, 30 left; the peak zone: left 380 times (a blow took a fighter below his line), re-entered 150 times (by refill)
  [summary] Athletics fighter-time by f (the share of his peak line left): peak (f 1) 71.0%, f 0.5-1 16.0%, f below 0.5 9.0%, empty (f 0) 4.0% of 52000 fighter-seconds
  [summary] Athletics heroes: 2 flagged, 2 party leaders (you, Arcor); lowest a hero reached: Arcor 12.5 of 80
  [summary] Athletics you: skill 90 → pool 90; 25 blows, 1 exhaustion, lowest 0.0 of 90
  [summary] Athletics your formations at the end: 1 Infantry 61 ± 14 (38 men) f avg 0.71, 9 at full strength, health avg 74% | …
  [summary] Athletics health cap: 120 cuts (a wound pulled Athletics down to the health left), biggest 60.0 points, 2400 points in all
  [summary] Athletics regen: 9000 fighter-seconds refilling - at a walk or slower (effort up to 0.40) 7000 s at the walking rate (x1), faster 2000 s at avg x0.71; refills to the top: 38 to full, 12 to a wound's cap; refill curve: flat (RegenRateNearFullPercent 100)
  [summary] Athletics refill from empty to the peak line (no blow between): 14 runs, avg 51.3 s (fastest 45.1 s, slowest 66.2 s) - 45.0 s at a walk or slower with these settings, longer while moving faster than a walk
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
  row (the empty row now at x0.30 - step 22; x0.60 in step 20b, x0.70 in step 14); health-cap cuts after a real fight; regen by effort and
  its curve; **`refill from empty to the peak line`** — the avg never below the "at a walk" time (a
  shorter one → the curve is off, tell Claude), close to it when men stood and walked, longer when they
  ran (step 14); **`walk vs run speeds`** — the ratio that
  tunes *Walking pace* (send it along: the setting should sit at, or a little above, walk/top on foot);
  `speed updates` in the hundreds or low thousands for a big battle and `held a tick` 0 or small;
  **`Athletics tick cost` avg well under 1 ms** in a 500+ battle (above 2 ms → tell Claude); detection:
  `outside one` small next to `during a counted release`, `shots seen` ≈ `ranged releases seen by the poll`;
  **`blocks paid by the defender`** (step 22) — all three kinds above 0 after a melee (`shield WRONG side 0` in a
  big fight → the side is never read: tell Claude), `other AI` carrying most of it, `free` and `while Athletics was
  off` 0 unless you set a price to 0 or switched off; errors none.

### L5. Attack rate — PAUSE ONLY (step 13)

A tired fighter's attack speed m sets **the pause after each attack**: an attack of D seconds (its
wind-up + release; ranged + the reload after the loose) is followed by D × (1/m − 1) with no new attack
(under 0.1 s: none). **You** (*Your attacks wait out the pause*): the input gate swallows your attack
presses until it ends; held, the attack starts when it does. **The AI** (*Tired AI wait out the pause*):
melee and ranged, on foot and mounted - since step 16 **by input** (*Tired AI keep their guard up (new
way)*: only the attack bits taken out of his own controls, a guard raised when he wants to attack; the
old NoAttack when off), and **the pause survives a step back**. **The animations** slow a little in a
straight line by f (step 20b: *Attack animation speed when empty (%)* 85 - x0.85 at empty, full speed at the
peak line; 100 = full speed always), and **D is measured at full animation speed** (played seconds × the
animation multiplier), so the pause in seconds is what it was; **the AI's decisions** (*Tired AI also decide
to attack less*) are off by default. Blocking, parrying, kicks, moving and weapon switches are never held.
- `[rate] attached: your attack gate FIRST in the behaviour list (0 of N; … right after MissionMainAgentController at K) - …` (mission start),
  `[rate] mission start: ON - PAUSE ONLY: animations x (0.85 + 0.15 f): full speed at the peak line, x0.93 halfway, x0.85 empty (AttackAnimationMinPercent 85); after each attack no new attack for D x (1/m - 1) (D = its wind-up + release, ranged + its reload, at full animation speed - the pause in seconds does not grow with the slower swing): you (AttackRatePlayerTimer) on - …, AI (AttackRatePaceHold) on - by input (AttackRatePaceByInput on: only the attack bits taken out of his own input, his guard his own, raised when he wants to attack - AiHoldRaiseGuard on), melee and ranged, on foot and mounted; AI decisions (AttackRateAiDecisions) off; never held: blocking, parrying, moving, weapon switches, kicks - read live; …`
  (the old way: `… AI (AttackRatePaceHold) on - NoAttack (AttackRatePaceByInput off: the engine's no-attack flag, step 13's technique), …`)
- Once: `[rate] first slowed fighter this mission: …` (C2), `[rate] first AI timer this mission: …` and
  `[rate] first AI timer ended at … s after … s - time up; …`, `[rate] your attack gate holds for the first time: …`;
  yours: the `[athletics] YOU: first attack pause …`, `… first attack pause ended …`, `… attack pressed …`,
  `… your held attack button started the next attack …` lines (C1, C1b).
- Verbose: `~[rate] AI timer: …`, `~[rate] AI timer ended: …`, `~[rate] AI timer not started: …`.
- Switches mid-battle: `[rate] AttackRatePaceHold switched OFF mid-mission at … s: N held fighters may attack again at once`,
  `[rate] the AI timer is ON again at …`, `[rate] AttackRatePlayerTimer switched OFF / ON …`, `[rate] AttackRateAiDecisions switched ON …`,
  `[rate] AttackAnimationMinPercent now 50 at … s: N tired fighters get their attack animations x (0.50 + 0.50 f): … - a slower swing on top of the timer; the pause in seconds unchanged`; step 16: `[rate] AttackRatePaceByInput switched OFF mid-mission at … s: new AI timers use NoAttack (the engine's flag); the N running now finish the way they began`,
  `[rate] AiHoldRaiseGuard switched OFF mid-mission at … s: a held AI man who wants to attack only has the attack taken out (from the next frame)`.
- Summary (numbers made up):
  ```
  [summary] attack rate, melee, AI, peak (f 1): animations asked x1.00 - wind-up 0.32 + held 0.08, swing 0.52 (clean, hit nothing 0.60), recoil after a block 0.40, pause 0.45 | cycle 1.40 s (n 900), m 1.00 - the fresh reference
  [summary] attack rate, melee, AI, f 0.5-1: animations asked x0.96 - wind-up 0.33 (x1.04) + …, swing 0.55 (x1.06) …, pause 1.60 (x3.56) | cycle 2.49 s (n 300), m 0.72 → target 1.98 s (incl. +0.04 s of slower swing): 126% - too slow
  [summary] attack rate, melee, AI, f 0.5-1 - timer: 310 (D avg 0.84 s at full animation speed - played 0.88 s, the slower swing at m 0.72 → asked avg 0.33 s = D x (1/m - 1)); measured: the next attack began avg 1.55 s after the attack's end (n 290), 1.22 s after the timer ended; started before the timer ended: 0 (must be 0)
  [summary] attack rate, melee, AI, empty (f 0): animations asked x0.85 - wind-up 0.38 (x1.18) + …, pause 4.60 (x10.2) | cycle 5.65 s (n 400; with a step back inside 5.95 s n 360, without 2.95 s n 40), m 0.20 → target 7.15 s (incl. +0.15 s of slower swing): 79% - too fast
  [summary] attack rate, melee, AI, empty (f 0) - timer: 450 (D avg 0.84 s at full animation speed - played 0.99 s, the slower swing at m 0.20 → asked avg 3.36 s = D x (1/m - 1)); measured: … 4.40 s after the attack's end (n 400), 1.04 s after the timer ended; started before the timer ended: 0 (must be 0); the timer's floor D/m avg 4.35 s - the cycle vs it: 130% (n 400), with a step back inside 137%, without 68% (at least ~100% = held as the spec asks)
  [summary] attack rate, melee, AI - verdict: OFF TARGET in 1 of 3 tired bands (…)
  [summary] attack rate, melee, you, empty (f 0): animations asked x0.85 - … | cycle 4.25 s (n 12), m 0.20 → target 4.90 s (incl. +0.15 s of slower swing): 87% - on target
  [summary] attack rate, melee, you, empty (f 0) - timer: 14 (D avg 0.82 s at full animation speed - played 0.96 s, the slower swing at m 0.20 → asked avg 3.28 s …); measured: … 3.30 s after the attack's end (n 12), 0.02 s after the timer ended; started before the timer ended: 0 (must be 0)
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
  1. **No slow-mo, only a heavier swing** (step 20b): `animations asked` x1.00 at the peak, ~x0.96 in f 0.5-1,
     ~x0.89 below 0.5, x0.85 empty (with *Attack animation speed when empty* 85), and each band's wind-up / swing /
     draw / reload about `(x 1 / that)` of the peak's - x1.18 at empty (a hit swing may run a little longer -
     `clean` is the pure one). **The pause is unchanged**: the timer rows' `D avg` stays the full-speed attack
     (~0.8 s melee, as before 20b) and `played …` beside it is the longer time on the clock; `asked avg` = D avg ×
     (1/m − 1) from the FULL-SPEED D. A `D avg` that grew with the slower swing → tell Claude.
  2. **The timer holds**: every `… - timer:` row `started before the timer ended: 0 (must be 0)`; `your
     timer` → `attacks that started while held anyway: 0`; `AI timer ends` → `an attack started anyway`
     about 0. Above 0 → the engine ignores NoAttack (AI) or the input gate's order is off (you) - tell Claude.
  3. **Its size**: `asked avg` = D avg × (1/m avg − 1) in every row; `measured … after the attack's end` ≥
     asked; `after the timer ended` = your fingers (near 0 when you hammer) or the AI's own re-decision.
  4. **Hold-to-attack**: `your attack began avg 0.0x s after` near 0.
  5. **The rate** (the `verdict`): target = the peak's cycle ÷ m, + the slower swing's own time (step 20b:
     `incl. +0.15 s of slower swing` - the band's played attack × (1 − its animation), so the heavier swing never
     reads as "too slow"). Your rows near target when you
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

### L6c. Battle pace — the shield wall and the archers (step 21)

- Mission start: `[rate] battle pace (step 21) at mission start: ON - AI shield infantry swing 30% less
  (ShieldInfantrySwingsLessPercent), other AI foot melee swing 15% less (FootMeleeSwingsLessPercent) - after each
  such swing a pause that stretches his expected cycle (his attack + his tired pause + his own gap AiMeleeGapSeconds
  1.00 s) ÷ (1 - the share), at full strength or tired; AI bowmen wait 2.0 s more after each shot (…), crossbowmen
  wait 2.5 s more after each shot (…), on foot and mounted, on top of the tired pause; riders' melee, thrown
  weapons, slings and you: tiredness only; …` (a slider at 0: `… unchanged (ShieldInfantrySwingsLessPercent 0 = off)`).
- A change mid-battle: `[rate] battle pace changed mid-mission at … s: … - applies at each fighter's next attack;
  a pause running now keeps its length`.
- The first attack of each class, once per battle: `[rate] battle pace - first <class> attack this mission: <name>
  at … s (<what the class is>; <its setting>) → a pause of X s asked = the tired pause T s + the battle-pace share
  S s - …` or `→ no pause - at full strength, and no battle-pace share (none for this class, or its setting at 0)`.
- The mission's first AI timer names its parts: `… → no new attack for 0.76 s = the tired pause 0.00 s (D x (1/m -
  1)) + 0.76 s of battle pace (shield infantry swing 30% less), until … s`.
- Summary — 7 lines (numbers made up):
  ```
  battle pace (step 21) settings at the end: ON - AI shield infantry swing 30% less (…) …
  battle pace - shield infantry (AI melee on foot, a shield in the other hand; ShieldInfantrySwingsLessPercent 30 - asks 30% fewer swings): 2400 attacks by 180 men; pauses 2350 - at full strength 900 (the share alone), tired 1450 (tired + the share), tired only 0; tired part avg 1.10 s, the share avg 1.20 s; no pause 50 (…); cycle 3.60 s (n 1900) = 16.7 swings a minute per man while fighting; his own gap after a pause avg 1.05 s (n 1800 - the model assumes 1.00 s, AiMeleeGapSeconds: shorter = part of the pause hid in his own idle time); the model (his attack + the pause + his own gap): with the setting at 0 2.55 s = 23.5 a minute, with it 3.64 s = 16.5 a minute (30% fewer) - measured ÷ it 99%: on target
  battle pace - other foot melee (AI melee on foot without a shield - …; FootMeleeSwingsLessPercent 15 - asks 15% fewer swings): …
  battle pace - riders (AI melee on horseback; no setting - tiredness only): … cycle … = … swings a minute per man while fighting
  battle pace - bowmen (AI bow shots, on foot and horse archers; ExtraPauseAfterBowShotSeconds 2.0 - sized for about 30% fewer shots): 900 shots by 60 men; pauses 900 - at full strength 200 (the share alone), tired 700 (tired + the share), tired only 0; …; cycle 7.80 s (n 800) = 7.7 shots a minute per man while shooting; his own gap after a pause avg 1.10 s (n 780); the extra inside a cycle avg 2.00 s - if none of it hid in his own gap, without it the cycle would be 5.80 s = 10.3 a minute: at most 26% fewer shots (the same battle with the slider at 0 is the real check)
  battle pace - crossbowmen (AI crossbow shots, on foot and mounted; ExtraPauseAfterCrossbowShotSeconds 2.5 - …): …
  battle pace - thrown and slings (AI javelins, throwing axes and knives, stones, slings; no setting - tiredness only): …
  ```
  Proves: **the same battle with the sliders at 0 and at their values - each class's "a minute per man"
  about 0.70 (shield infantry, bows, crossbows) and 0.85 (other foot melee) of the run at 0**; riders and
  thrown weapons unchanged; within a run: `measured ÷ it` on target and `his own gap` near `AiMeleeGapSeconds`
  (if it reads well below, part of the pause hid in the AI's own idle time - the cut fell short; tell Claude).
  The attack-rate lines change with it: the AI timer counts `peak (f 1)` holds now (`by f: peak (f 1) N, …`),
  each timer row adds `+ the battle-pace share avg S s`, and the band verdicts' fresh reference carries the
  share too (read the battle pace lines for step 21, the band lines for tiredness).

### L6d. Brace by orders (step 23)

- Mission start: `[brace] mission start: ON - an AI man whose Athletics bar (points ÷ his full pool, what the bar
  shows) is at or below his formation's order floor ± his own 10 (BraceFloorSpreadPercent, rolled once per man and
  battle, apart from the margin's roll - step 28) stops attacking in melee and only defends (guard up) until he
  refills to floor + 20% (BraceRecoverPercent) ± his own 5 (BraceRecoverSpreadPercent, rolled once per man and
  battle): charge 20% → 40% (…), advance 40% → 60% (…), hold / halt / retreat / move / follow / no order 60% → 80%
  (…), no formation (tournaments, arenas, duels) 20% → 40% (BraceFloorNoFormationPercent; step 28); a wound caps the target …; bows, crossbows and throws go on; the shield taken out …, held up …; AI heroes and
  riders too, never you - read live, every AI man looked at about every 0.25 s; …` (off: `OFF (BraceEnabled) - AI
  men swing at any Athletics`).
- A change mid-battle: `[brace] settings changed mid-mission at … s: …`; a switch off: `[brace] BraceEnabled switched
  OFF mid-mission at … s: N braces lifted at once - those men attack again` (or `the whole mod (ModEnabled)`).
- The first brace and its end in full (D7); with VerboseLogging every other one `~[brace] brace: …` / `~[brace]
  brace ended: <name> after … s - <why>, bar …%` (rate-limited, bucket `brace`).
- Summary — 8 lines (numbers made up; step 28 added `no formation` to `by floor` and the `brace floors` line):
  ```
  brace by orders (step 23): ON - … (the settings sentence)
  brace: 95 of 160 AI fighters braced, 210 braces - by order: charge 30, the AI's advance 60, stop / hold 120; by floor: charge 30, advance 60, hold and the rest 120, no formation 0; time bracing 2100 man-seconds = 14.6% of the AI fighters' time on the field (14400 man-seconds), avg 10.0 s a brace, 22.1 s per man who braced, the longest 48.3 s (Imperial Legionary)
  brace lengths: under 5 s 60, 5-15 s 110, 15-30 s 30, 30-60 s 10, 60 s or more 0; still bracing when the battle ended 12 (2 of them for 30 s or more) - many long braces and many still bracing at the end = lines that turtle (…)
  brace ends: refilled to floor + recover 150, the order changed 25, refilled to his wound cap 8, switched off 0, fell or left the field 15, the player took him 0, the hideout boss fight's fresh start 0, the battle ended 12, an error (lifted to stay safe) 0
  brace margins (BraceRecoverSpreadPercent 5): 160 men rolled, their offsets now min -5.0 / avg +0.1 / max +4.9 points on 20%; braces by his own margin: short (−) 70 (avg 8.9 s), middle 72 (avg 10.1 s), long (+) 68 (avg 11.2 s)
  brace floors (BraceFloorSpreadPercent 10, step 28): 160 men rolled, their floor offsets now min -9.9 / avg +0.2 / max +9.8 points on the order's floor; braces by his own floor: low (−, fights on longer) 60 (avg 9.5 s), middle 75 (avg 10.0 s), high (+, braces sooner) 75 (avg 10.4 s)
  brace shield (BraceWieldShield on): at the brace's start - already in hand 150, taken out 20, a one-hander first 15, no shield 20, a ranged weapon in hand (left alone) 5, two-hander and no one-hander (left alone) 0, the switch off 0; wield calls: the shield 40, a one-hander 15; the shield seen in his hand after we asked 30, the AI put it away again while bracing 6, never in his hand by the brace's end 5
  brace GUARD: melee hits taken while bracing 400, blocked 180 (45.0% - compare the AI holds GUARD line's everyone else); his attacks taken out of his input 5000 frames (a guard raised 4800), the shield held up 60000 frames (BraceRaiseShield on), ranged frames let through 300; melee attacks that started while bracing anyway 2 (should be about 0), ranged shots while bracing 20 (allowed); braces the engine never called our input hook during 0 (should be 0)
  ```
  Proves: the floors by order (`by order` / `by floor`, `the order changed` after a charge); the lines do NOT turtle
  (`brace lengths` mostly short, few `still bracing when the battle ended`) - if they do, the refill cannot beat the
  blocks' cost: lower `CostPerShieldBlock` or the floors; the shield sticks (`seen in his hand` ≫ `put away again`);
  bracing men block at least as well as everyone else (`brace GUARD` vs L6b's `everyone else`); the hook works
  (`braces the engine never called our input hook during 0`, melee started while bracing ~0). The ALT numbers
  and the orders strip show the bar; the "ready men" count per formation comes in the next step.

### L6e. Tired AI blocks in a random direction (step 30)

- Mission start: `[guard] mission start: tired block direction: on - below the peak line an AI man's block direction is
  partly random: weight w = 100% × (1 − f) (TiredBlockRandomPercent), so at f 0.5 50% of his picks and empty 100% are a
  random direction (right 1 in 4 by luck); through the game's own AiRandomizedDefendDirectionChance = own + (1 − own) ×
  w; full strength, you and men of the game's own choice untouched - read live; …` (off: `tired block direction: off
  (TiredBlockRandomPercent 0 | AthleticsEnabled | ModEnabled) - every AI man blocks as the game decides`).
- A change mid-battle: `[guard] settings changed mid-mission at … s: …`.
- The first raised chance in full (D8 step 5); with VerboseLogging every later recompute `~[guard] tired block
  direction: <name> f 0.40, w 0.60 - chance 0.12 → 0.65` (rate-limited, bucket `guard-chance`).
- Summary - 4 lines (numbers made up; the last two since step 34 - the guard split by what the DEFENDER holds as the blow
  arrives, read before the block's cost is charged):
  ```
  tired block direction (TiredBlockRandomPercent 100, step 30): the game's random-direction chance raised for 140 AI men (900 recomputes; 30 of them over a game value below 0 - never random before); his own → written, avg by f band: f 0.5-1 0.35 → 0.55 (n 500), f below 0.5 0.33 → 0.80 (n 300), empty (f 0) 0.30 → 1.00 (n 100)
  AI guard by f (melee blows that reached AI men; kicks, bashes, charges and missiles left out - step 30: tired men should block worse): peak (f 1) 900 blows - blocked 40.0% (shield right side 25.0%, WRONG side 5.0%, parry 10.0%), landed 60.0%; f 0.5-1 …; f below 0.5 …; empty (f 0) 200 blows - blocked 15.0% (…), landed 85.0%
  AI guard by f WITH a shield in hand (when the blow came - step 34): peak (f 1) 600 blows - blocked 50.0% (shield right side 37.5%, WRONG side 7.5%, parry 5.0%), landed 50.0%; …; empty (f 0) 120 blows - blocked 30.0% (shield right side 6.0%, WRONG side 20.0%, parry 4.0%), landed 70.0% (a wrong direction with a shield = a WRONG-side block: the shield still stops the blow)
  AI guard by f WITHOUT a shield (when the blow came - step 34): peak (f 1) 300 blows - blocked 20.0% (shield right side 0.0%, WRONG side 0.0%, parry 20.0%), landed 80.0%; …; empty (f 0) 80 blows - blocked 3.0% (…), landed 97.0% (a wrong direction without a shield = the blow lands: empty should land far more than peak)
  ```
  The first line's recomputes also say `N with a shield in hand, M without`.
  Proves: the chance is raised where the bar is low (`his own → written` climbing to 1.00 at empty) and the engine
  follows it (`AI guard by f`: blocked % falling band by band, `WRONG side` rising). In a field battle many blows land
  whatever the guard (flanks, backs), so compare bands, not absolute numbers; the tournament 1v1 is the clean test.
  Step 34: the WITH line answers "does a tired shield man block on the wrong side" (`WRONG side` up, `landed` barely
  moving - the shield still stops it); the WITHOUT line answers "does a tired shieldless man parry worse" (`landed` at
  empty well above peak). Read the WITHOUT line first - mixing the two hid it in the first tournament log.

### L6f. The swing stack (step 32)

- Mission start: `[rate] swing stack (step 32) at mission start: ON - every fighter stores swings: max(2, his Athletics
  points / 20, rounded) (SwingStackMinimum 2, SwingStackPointsPerSwing 20 - a full 150 pool holds 8, empty 2); an attack
  spends one and waits only when none is left; …` (off: `OFF (SwingStackEnabled) - step 13's pause after EVERY attack below
  the peak line`; the mod or Athletics off: `… - no pause at all`). Changes: `[rate] swing stack switched ON / OFF mid-mission
  at … s: …`, `[rate] swing stack settings now: …`.
- YOU (D9): `first burst this battle`, `swing stack empty at …` (the first in full, later ones rate-limited, bucket
  `athletics-stack`), `kick pressed at … with no stored swing (0/N) - refused`, `an attack at … started with NO stored swing`
  (should stay rare - a chained blow or a gate miss); the first-pause line ends `- your swing stack is empty: …`.
- Summary - 5 lines (numbers made up):
  ```
  swing stack: ON - every fighter stores swings: max(2, his Athletics points / 20, rounded) …
  swing stack - you: attacks 120 (from the stack 95, after waiting at 0 25, at an empty stack 0); bursts (2+ in a row, no swing back between) 14 by length 2: 6, 3: 4, 4: 3, 5-6: 1, longest 6; waits at 0 25 (avg 1.10 s, longest 3.20 s)
  swing stack - AI: attacks 5200 (from the stack 3900, after waiting at 0 1250, at an empty stack 50 - chained blows or a gate miss); bursts … ; waits at 0 1300 (avg 1.40 s, …)
  swing stack - kicks and bashes (KicksUseSwings on at the end): spent a swing 40 (you 12), refused at 0 - yours 3 (the gate took the kick key out), the AI's 0 input frames; started at 0 anyway 0 (…)
  swing stack - average fill (stored ÷ his cap) by f: peak (f 1) 100%, f 0.5-1 80%, f below 0.5 55%, empty (f 0) 30%; swings back 4100, cut by a points drop 900, fresh starts 0 - the attack rate lines' verdicts read over the whole fight now: …
  ```
  Proves: the stack is spent and refilled (`from the stack`, `swings back`), you burst (`bursts … longest`), waits happen
  only at 0, kicks are refused at 0. In the attack-rate block (L5) the AI timer's `not held` line gains `a stored swing
  left (step 32's swing stack) N`, and the band verdicts mix bursts (fast) with waits (slow) - judge the rate over the
  whole fight, or A/B with *Swing stack* off. The recovery bar's summary line (L7) adds `swing stack shown, fewest stored N`.

### L6g. The sprint (step 33)

- Mission start: `[sprint] mission start: ON - hold LeftControl (on foot, in a fight); the store = Athletics points / 20 s
  …; AI ON - on a NEW order he sprints what he holds above the order's reserve (stored - keep x his full store), nothing
  below 0.2 s (no 20% start gate for the AI): charge keeps 0%, advance keeps 50%, move keeps 80%; hold, retreat, fall back,
  follow none - read live; …` (step 37) (off: `OFF (SprintEnabled) -
  nobody sprints`, or the switch that holds it off). A change: `[sprint] settings changed mid-mission at … s: …`; a switch
  off with sprints running: `[sprint] SprintEnabled switched off at … s: N sprints ended, their speeds back over the next
  ticks …` (`SprintAiEnabled` = the AI's only).
- YOU (D10, bucket `sprint-you`): the first sprint in full (stored, T, the floor when it held the store up, the
  profile - the fresh-run peak, the fade target x1.10 of your current run -, the acceleration game → ours; step 35), later
  starts / ends rate-limited
  (`YOU: sprint at …`, `YOU: sprint ended at … (key released / store empty / control changed / …)`), the first refusal in
  full (below 20%, an empty bar, on a horse). AI: the first new order, the first AI sprint and (step 36) the first sprint a reserve held back in full; verbose `sprint-ai`
  / `sprint-order` lines with VerboseLogging.
- Summary - 4 lines (numbers made up):
  ```
  sprint (step 33): ON - hold LeftControl (on foot, in a fight); the store = Athletics points / 20 s (150 points = 7.5 s, never below 2.0 s (an empty bar still holds 2.0 s)) …
  sprint - you: 14 sprints (length asked avg 5.20 s, ran avg 2.80 s, longest 7.50 s); 3 started off the 2.0 s floor (his points alone stored less); refused: below 20% stored 3, an empty bar 0, on a horse 0; ends: key released 12, store empty 2
  sprint - AI: new orders seen - charge 4, advance 3, move 8, other 9; sprints - charge (keeps 0%) 310 men (1500.0 s in all, avg 4.84 s), advance (keeps 50%) 120 men (240.0 s in all, avg 2.00 s), move (keeps 80%) 140 men (120.0 s in all, avg 0.86 s); not sprinted: reserve kept 125 (charge 0, advance 30, move 95), less than 0.2 s or empty: charge 12, advance 3, move 20; off the 2.0 s floor 45; mounted 40, yours to drive 2; ends: its share used 300, an enemy within 3 m 150, the order changed again 60, …; melee checks 9000
  sprint speed check (x his FRESH run speed = his top at the sprint's start ÷ his run multiplier then, while it ran): 4000 samples - asked avg x1.62, engine top avg x1.61, max x2.00, off by more than 0.05: 120 - the engine's top speed follows the sprint - no cap above the run speed; acceleration while sprinting: the game's top-speed reach avg 2.55 s → ours 1.00 s (sprint starts: 540); 6500 recomputes for the sprint, stores clamped by a points drop 800, refilled 9000.0 s of store
  ```
  Proves: you sprinted and let go (`ends: key released`), the 20% gate bites, the AI sprints on new orders (charge / advance
  / move men - step 37: moves short, `reserve kept` the back-to-back moves that found nothing above 80% and the advances nothing above 50%), stops in melee, and the ENGINE honours the run property above its base (`engine top` ≈ `asked`, both × the
  FRESH run since step 35 - a tired man's peak reads x2.00 too). The acceleration clause shows the game's own
  TopSpeedReachDuration (2.3-2.8 s expected) against ours; the floor counts say how often a tired store was held up. The HUD block (L7)
  adds `hud: sprint bar - sprints shown N, … s on screen sprinting, … s below the start mark`. The Athletics `run speed
  check` lines leave sprinting samples out (they would read as "above the curve").

### L6h. The shield wall holds its slots (step 41) and its small step out (step 42)

- Mission start: `[wall] mission start: ON (ShieldWallHoldsSlots) - … - read live; written right after vanilla's own update of
  each man (his ~0.5 s timer) and at a look every 0.25 s; …` (off: `OFF (ShieldWallHoldsSlots) - vanilla: …` or
  `OFF (ModEnabled)`). The switch mid-battle: `[wall] ShieldWallHoldsSlots switched OFF mid-mission at … s: N men given
  vanilla's own value back at once …` / `… switched ON mid-mission at … s: …` (the master switch: `the whole mod (ModEnabled)`).
- The first man held in full (`[wall] first man held in his slot this mission: …`). Only if a game update renamed the timer:
  `[wall] vanilla's update timer (Agent._cachedAndFormationValuesUpdateTimer) cannot be read on … - … rewritten every 0.10 s
  instead (tell Claude)`. Verbose (bucket `wall`): `… let go at … s: a charge order - vanilla's own value given back`.
- Summary - 5 lines (numbers made up):
  ```
  shield wall holds its slots (step 41): ON (ShieldWallHoldsSlots) - every AI man on foot in a shield wall, square or circle keeps his slot … - switched 2 time(s) during this battle (the ON / OFF lines below split the samples by it)
  wall: 80 men looked at, 38 held in their slots at some point; looks - held 9000, left to vanilla: switched off 2400, you or a man you drive 300, mounted 0, another arrangement 6000, a charge order 900, far from his slot and behind the line 12
  wall writes: 27000 "keep your slot" writes - right after vanilla's own update 9100, by the 0.25 s look 17900, by the fallback 0; vanilla's own updates seen on held men 9100 (one every 0.50 s per man - the game's timer is 0.45-0.55 s), men whose update timer could not be read 0 (should be 0 - else the fallback); released to vanilla's own value 80 (switched off 38, a charge order 40, far from his slot and behind the line 2); held men who fell or left the field 9, still held when the battle ended 0; errors 0; cost avg 0.004 ms a tick, max 0.05 ms
  wall slots ON: 4500 samples of AI men in a holding shield wall / square / circle: more than 1 m ahead of his slot 2.0%, more than 2 m 0.5%, mean ahead 0.15 m (behind his slot counts 0), the farthest 2.6 m; in contact (an enemy within 5 m) 900 samples: more than 1 m ahead 4.0%, more than 2 m 1.0%, mean ahead 0.25 m, the farthest 2.6 m
  wall slots OFF (vanilla): 1200 samples …: more than 1 m ahead of his slot 15.0%, more than 2 m 8.0%, … in contact … 400 samples: more than 1 m ahead 40.0%, more than 2 m 22.0% … - compare with the ON line: the hold works when ON's "more than 1 m ahead" (in contact above all) is well below OFF's
  ```
  Proves: the hold reaches the men (`held` looks, writes `right after vanilla's own update` ≈ `vanilla's own updates seen`,
  one every ~0.5 s - the timing of RESEARCH §O working in game), and that it WORKS: `in contact` "more than 1 m ahead" ON
  well below OFF. The left-to-vanilla reasons show who was not held and why (a line formation = "another arrangement").
- Step 42 adds a SIXTH line (numbers made up) and `, by the step-out leash N` in the writes line:
  ```
  wall step out (step 42): leash 1.0 m now - 240 steps out by 31 men (2.10 a man a minute while held, out of their slots 6.5% of the held time); each step's farthest out: mean 0.80 m, the farthest 1.30 m; ended: reached the leash 150, enemy gone 80, let go to vanilla 9, fell or left 1; reined at the leash 0.12 m past it on average, at most 0.30 m (the 0.1 s look); back in their slots after a step 140; in contact past the leash 3.0%, past it + 0.5 m 0.2% (of 900 in-contact samples with the leash on - the overshoot: should be near 0)
  ```
  Proves: men step (`steps out by … men`, a few a man a minute), the leash bites (`reined … m past it` ~0.1-0.3 m, `past it +
  0.5 m` near 0) and they come back (`back in their slots after a step`). With L 0 all battle: `- the leash was 0 (the hard
  hold of step 41) the whole battle: no steps`. Verbose (bucket `wall-step`): `… steps out at … s` / `… step out ended at …`.

### L6i. Eagerness by Athletics and the fever (step 43)

- Mission start: `[fever] mission start: aggression: … ; fever: … ; fever sprint: … - read live; every man looked at about every
  0.25 s; …` (off: `aggression: off (ModEnabled | AthleticsEnabled) - the game's own attack decisions; fever: off (…)`). A change:
  `[fever] settings changed mid-mission at … s: …`; the fever switched off: `[fever] FeverEnabled switched off at … s: N fevers ended …`.
- First-time lines in full: decision values written, fever armed, burning, fever sprint, faded (D13). Verbose: `~[fever] … armed /
  fever sprint / fever ended` (buckets fever-arm, fever-sprint, fever-end).
- The [summary] lines: `fever settings: …`; `fever armed: N (charge … - by you … / by the AI general …; advance …); … re-armed …
  kept … ignored …; not armed: on a horse …, no Athletics …, player-controlled …`; `fever contact: B of N fevers met an enemy
  within 5.0 m - armed → contact mean … s, max … s; never met one (ended still armed): …`; `fever burn: faded …; burn length with
  a shield in hand (most of it) …, mean … s from a mean store of … s (x0.70); without … (x1.00) …; all ends: …`; `fever
  recomputes: … asked … over … AI man-minutes on foot = … a man-minute; per fever …`; `fever sprints: started … (fever left
  100-67%: …, 67-33%: …, 33-0%: …) in … checks; refused: missed the roll …, reserve kept …, no store …, sprinting already …, in
  melee …, no enemy in range …, sprint off …`; `fever proof: AI melee attacks per man-minute in contact …, by bar 100-75 / 75-50 /
  50-25 / 25-0 %: in a formation, no fever a / b / c / d (…); in a fever … - in a fever x… the rate without`; `fever proof, no
  formation (tournaments, arenas, duels - no orders, no fever): …`; `fever values written (AI on foot, the game's → ours): attack
  chance bar 100-75% 0.096 → 0.056 (x0.58, n), …, in a fever …; riposte chance …`; `fever cost: … looks over … men, … µs a look`.

### L6j. The impact spike (step 47)

- Mission start: `[impact] mission start: spike OFF (ImpactSpike) - nothing of the impact system runs (vanilla) - read live` - or, on:
  `[impact] mission start: SPIKE ON (ImpactSpike - a test tool, not gameplay): fixed loud values prove each engine lever - 1) … 6) …;
  the game's stun cap (StunPeriodMax) 0.80 → 2.00 s - read live (…; PLAYTEST D16)`. Switched mid-mission: `[impact] spike switched
  ON mid-mission at … s: …` / `[impact] spike switched OFF (ImpactSpike | ModEnabled) mid-mission at … s: vanilla at once - the game's
  stun cap back to 0.80 s, N queued action(s) dropped unplayed, N watch(es) and N shield(s) no longer followed`.
- First-time lines (one each per battle): lever 1 `first KnockDown answered`, `first Fall seen after a KnockDown kick` / `a KnockDown
  kick … showed NO Fall`; lever 3 `first reaction slowed`, `first slowed reel measured`; lever 4 `forced stagger #1 …` (and the first
  accepted one), `first forced stagger played out`; lever 5 `first shield drop queued`, `first shield left the hand`, `first dropped
  shield picked up again`; lever 6 `first blow of our own`, `first Fall seen after our own blow` / `our blow … showed NO Fall`. A lever's
  error: `[error] impact.<site>` (once, with its stack) + `[impact] spike lever N (<name>) disabled for this mission after an error at
  <site> - the other levers run on …`.
- The [summary] lines (D16 reads them): the header `impact spike (step 47, ImpactSpike - a test tool): on N% of the battle …; levers
  disabled by an error: …; engine calls …; watch pool full …; cost … ms a tick`, then one line per lever `impact spike lever N - <what>:
  <asked, seen, measured control → treated, refusals> → WORKS | FAILS | UNSEEN - <what it means for steps 48-53>`. Never on in that battle:
  one line `impact spike (step 47): off all battle (ImpactSpike - a test tool, Advanced) - nothing asked, nothing changed`.

### L6k. The impact model (step 48)

- Mission start: `[impact] mission start: the model (step 48 - IMPACT I2, docs/IMPACT.md) LIVE - kicks / bashes applied, blocks, clean hits,
  the riposte decided and logged only (effect not built yet) (step 50; before it: `LIVE - but no effect is built yet …`); families: kicks / bashes on, blocks on, clean hits on, riposte on; anchors - …; limits
  - …; against you 100%; a rider knocked off at 50% of a knockdown - read live; …` (or `SHADOW (ImpactShadowMode)`, `SHADOW (the spike is
  on - ImpactSpike owns the levers)`, `OFF (ModEnabled | AthleticsEnabled | ImpactEnabled) - the game's own reactions`). Mid-mission: `[impact]
  the model switched ON | OFF (<switch>) mid-mission at … s: …`, `[impact] the model's mode changed mid-mission at … s: SHADOW … | LIVE …`.
- First-time lines: `[impact] first <kick | shield bash | right-side block | parry | wrong-side block | clean hit> decided at … s - <one
  decision in full: the bar and T, points and bodies, impact vs guard, skills → R (log2, beyond the band), reeling, damage → x: each outcome's
  chance (cut for N recent) → rolled … YES | no, or - refused: <on the ground | knockdown immunity | stagger immunity | no shield in hand |
  a rider | replaced>; the reel / stun / get-up / stagger / recoil lengths>`; `[impact] first time <knocked down | shield dropped | staggered |
  riposte> WOULD happen | happens, at … s - …`. A failing engine read: `[error] impact.read` (once, with its stack) - counted "the engine
  read failed". Verbose (bucket `impact`): every decision in the same words.
- The [summary] lines (D17 reads them): `impact (step 48 - the model; <LIVE - kicks / bashes applied (N decisions) | LIVE, but nothing applied this mission … | SHADOW | MIXED>): N decisions -
  kicks …, shield bashes …, right-side blocks …, parries …, wrong-side blocks …, clean hits …; YOU took …, dealt …; not decided: off …, its
  family switched off …, not tracked …, the engine read failed …; N landed blows decided in the damage decorator's KnockDown seam (one
  decision per event)`; per kind `impact <kind> by the <kicked | blocker's | hit> man's bar 100-75% / 75-50% / 50-25% / 25-0%: N = a / b / c
  / d; mean x …; would <knock down | drop a shield | stagger> … = N (rolled …, mean chance …)`; `impact riposte (… by the attacker's bar …)`;
  `impact limits: refused - …; diminished: …; riders would be knocked off N`; `impact lengths asked: …`; `impact cost: N decisions, … µs a
  decision (max …) - … in shadow, … live (… applied)`. Never on: `impact (step 48 - the model): OFF all mission (<switch>) - nothing decided`.

### L6m. Kicks and bashes LIVE (step 50, IMPACT I3)

- Mission start: the model's line (L6k) read `LIVE - kicks / bashes applied, blocks, clean hits, the riposte decided and logged only
  (effect not built yet)` in step 50 (since step 51: L6n). At game start: `[impact] item pickup model decorator registered over <model> - an AI man may not pick a shield up
  again for ShieldPickupLockSeconds …` (or `this game type has no ItemPickupModel …`).
- Every kick / bash YOU deal or take: `[impact] YOU: your <kick | shield bash> on <him> | <him>'s <kick | shield bash> on you (<his | your>
  bar N%, x …): knocked down (N%) [, shield dropped (N%)] - gets up xN slower | no knockdown (N% [- <refusal>]), shield kept (N% | no shield in
  hand) - reels xN`.
- First-time lines: `[impact] live: first shield drop made from the tick at …`, `… first knockdown seen at …`, `… first <reel |
  fall-and-rise> slowed at … at speed …`, `… first get-up measured at …`, `… first slowed reel measured at …`, `… first shield left the hand at
  …`, `… first knocked-away shield picked up again at …`, `… a knockdown answered at … showed NO Fall within 1.0 s …`. A lever's error:
  `[error] impact.<model-knockdown | live-drop | live-slow | live-watch | live-shield>` once with its stack + `[impact] live lever <name>
  disabled for this mission after an error at …`. Switched off / shadow mid-mission: `[impact] kicks no longer live at … s (<mode>): N queued
  shield drop(s) dropped unplayed, M watch(es) let go`.
- The [summary] lines (D19 reads them): `impact live (step 50 - kicks and bashes; <mode>): …`, `impact live lengths: …`, `impact live shields:
  …`, `impact live limits: …` (L6k's `impact <kind> by …` lines say `did` for the kicks and bashes). Nothing live: `impact live (step 50 -
  kicks and bashes): no live kick or bash this mission (<mode>)`.

### L6n. Blocks and the riposte LIVE (step 51, IMPACT I4)

- Mission start: the model's line (L6k) reads `LIVE - kicks / bashes, blocks, the riposte applied, clean hits decided and logged only (effect
  not built yet)`; at the first tick `[impact] live: blocks live at … s - the game's stun cap (StunPeriodMax) 0.80 → 2.00 s while they are …`.
  Blocks stop (off / shadow / the spike / the master switch): `[impact] blocks and the riposte no longer live at … s (<mode>): N queued
  action(s) dropped unplayed, M watch(es) let go …` and once `[impact] live: blocks no longer live at … s - the game's stun cap back to 0.80 s`.
- Every live block outcome YOU deal or take: `[impact] YOU: your <right-side block | parry | wrong-side block> of <him>'s <weapon> (your bar
  N%, x …): you are knocked down (N%) | you are staggered (N%) | your shield dropped (N%) [- N.NN s] [- gets up xN slower]` / `… <him>'s
  <kind> of your <weapon> (his bar …): he is staggered (N%) …`; the riposte: `[impact] YOU: <him> blocked you (<kind>): you reel (riposte,
  N%) - N.NN s (x_r …)` / `YOU: your <kind> of <him>'s <weapon>: he reels (your riposte, N%) - …`.
- First-time lines: `[impact] live: first block stagger - <him> after his <kind> - act_stagger_backward from the tick at … s (… after the
  block), <with | OVER> the engine's priority, speed … for … s asked (the animation's own length … s - <measured | a guess until one is
  measured>)`, `… first RIPOSTE - the attacker <him> staggered after <blocker>'s <kind> …`, `… first block stagger seen at …`, `… first <block
  | riposte> stagger measured at … - lasted … s (asked … s at speed …); no new stagger on him for 2.0 s`, `… first shield drop after a
  WRONG-side block made from the tick at …`, `… first BLOCKER knocked down - our own blow (RegisterBlow, KnockDown | KnockBack, 1 damage) on
  <him> …`, `… first blocker's Fall seen at …`, `… a block stagger the engine took at … never showed on <him> …`. A lever's error: `[error]
  impact.<live-stun | live-stun-cap | live-stun-snapshot | live-stagger | live-own-blow>` once with its stack + `[impact] live lever <the block
  stun by tiredness | the forced stagger (blocks, the riposte) | the blocker's knockdown (our own blow)> disabled for this mission after an
  error at …` (the stun's comes from a worker thread - logged at the next tick).
- The [summary] lines (D20 reads them): `impact live <right-side shield blocks | parries | WRONG-side shield blocks> by the blocker's bar …`,
  `impact live block staggers: …` (with `BLOCKS MADE FROM INSIDE A STAGGER`), `impact live blockers knocked down: …`, `impact live riposte
  (a fresh blocker staggers the ATTACKER) by the attacker's bar …`, `impact live block stun: …`, `impact live block limits: …`. Nothing live:
  `impact live blocks (step 51 - blocks and the riposte): no live block this mission (<mode>)`. Since step 52 the mode reads `LIVE - kicks /
  bashes, blocks, clean hits, the riposte applied`.

### L6o. Clean hits, the reeling bonus and MOMENTUM LIVE (step 52, IMPACT I5)

- Mission start: the model's line (L6k) adds `tired men always flinch on; momentum (the attacker's run into the blow) +60% impact at 7.0 m/s
  and faster, none at 2.0 m/s or slower` (+ the riders' part since step 53 - L6p). Clean hits stop (off / shadow / the spike / the master switch): `[impact] clean
  hits no longer live at … s (<mode>): N watch(es) let go - a slowed flinch / fall ends by itself`.
- Every decision line (first of a kind / verbose) shows the momentum when there is one: `impact 2.01 (3.0 kg, speed 80 x1.00, cut) incl.
  momentum x1.60 (8.0 m/s into the blow) vs guard …`.
- Every live clean hit that DID something to you or that you did: `[impact] YOU: <him>'s blow on you: you are knocked down | you are staggered
  (N%; your bar N%, x …[, reeling][, momentum x…]) - gets up xN slower | - N.NN s` / `YOU: your blow on <him>: he is …`.
- First-time lines: `[impact] live: first clean hit that <KNOCKS DOWN | STAGGERS> at … s - <attacker>'s <class> blow on <him> (his bar N%, x
  …[, REELING +0.75][, momentum x… at … m/s]): KnockDown answered in the decorator's seam - his fall-and-rise to be slowed x… | his own flinch
  to be slowed to … s (x… the flinch's … s - <measured | a guess until one is measured>)`, `… first clean-hit knockdown seen at …`, `… first
  clean-hit flinch seen at …`, `… first clean-hit flinch slowed at …`, `… first clean-hit stagger measured at …`, `… first shrug-off REFUSED
  at …`, `… a clean-hit knockdown answered at … showed NO Fall within 1.0 s on <him> …`. A lever's error: `[error] impact.live-shrug-off` (or
  `impact.shrug-off-hook`) once with its stack + `[impact] live lever tired men always flinch (the shrug-off refused) disabled for this mission
  …`; the knockdown / slow / watch levers are the kicks' (L6m).
- The [summary] lines (D21 reads them): `impact momentum (step 52 - …)` (always - every family's decisions by the attacker's speed band),
  `impact live clean hits, <class>, by the hit man's bar …` (one per weapon class that was hit, cut always), `impact live clean-hit
  knockdowns: …`, `impact live clean-hit staggers (the engine's own flinch slowed): …`, `impact live shrug-offs (…): …`, `impact live
  clean-hit limits: …`. Nothing live: `impact live clean hits (step 52): no live clean hit this mission (<mode>)`.

### L6p. Riders and the on-screen lines LIVE (step 53, IMPACT I6)

- Mission start: the model's line (L6k) now ends `…; momentum … none at 2.0 m/s or slower; a rider's horse +30% at 10.0 m/s, none at 3.0 m/s
  (a couched lance never)`. A decision line with a rider's momentum: `incl. momentum x1.30 (his horse at 10.0 m/s into the blow)`; a couched
  lance: `(a couched lance - no momentum)`.
- First-time lines: `[impact] live: first rider KNOCKED OFF his horse at … s - <attacker>'s <event> on <rider> (his bar N%, x …, the knockdown's
  chance x 50% = N%[, momentum x…]) - answered in the damage decorator's dismount seam (DecideAgentDismountedByBlow)`; `… first BLOCKING rider
  knocked off his horse - our own blow (RegisterBlow, CanDismount, 1 damage) on <him> after his <block> of <attacker>'s blow, from the tick at …
  s (0.0x s after the block)`; `… first rider SEEN off his horse at … s - <him>, N.NN s after <the way>`; `… a rider still on his horse 2.0 s
  after <the way> (<him>, at … s) - the engine did not play the dismount; tell Claude if the summary shows many (PLAYTEST D22)`.
- YOU: `[impact] YOU: <him>'s <event> on you (riding) (your bar N%, x …): you are knocked off your horse (N%)[, your shield knocked away (N%)]`
  / `YOU: your <event> on <him> (riding) (his bar …): he is knocked off his horse (N%)…`.
- Every on-screen line is logged as shown: `[impact] on screen at … s (#D65252FF | #AE4AD9FF | #FFFFFFFF): <the line>` - red = done to you,
  purple = done by you, white = your side's heroes (ImpactMessagesIncludeYourMen).
- A lever's error: `[error] impact.live-dismount-blow` (our blow), `impact.model-dismount` (the seam's decision) or `impact.dismount-hook` once
  with its stack + `[impact] live lever a rider knocked off his horse disabled for this mission …`; `impact.message` / `impact.messages-tick`
  for the lines (no line, nothing else changes).
- The [summary] lines (D22 reads them): `impact riders (step 53 - …): live decisions on riders …; dismounts DECIDED … → APPLIED … → SEEN …`
  (or `impact riders (step 53): no live decision on a rider this mission (…)`), `impact riders' shields and limits: …`, `impact messages (step
  53 - on-screen lines, …): offered … → lines SHOWN …, parts MERGED …`; the momentum line's `|| RIDERS (step 53 - by the horse's speed into
  the blow; …): standing … | walking … | trotting / cantering … | galloping … | couched lances (no momentum - the game's own big hit) N`.
- Step 53 also changed: a rider's block stun is lengthened by his tiredness like anyone's (L6n's stun counts include riders now); the
  `impact limits` line reads `riders knocked off their horse by the dice N (step 53: applied / seen in the 'impact riders' line)`.

### L6q. The AI punishes a window (step 54, IMPACT I7)

- Mission start: `[impact] mission start, the AI punishing a window (step 54): ON - an AI man on foot whose enemy reels or lies down (a live
  impact outcome, you too) decides to attack at 100% of the game's AI (never lower than his own share) until the window closes + 0.5 s; a timid
  man within 3.0 m steps in for it; never you, never a rider - read live; …` (OFF: `OFF (<the switch>)[ - the windows are still measured]`);
  a change: `[impact] punish settings changed mid-mission at … s: …`.
- First-time lines: `first window this battle: …`, `first AI man pressing a window this battle: …`, `first window punished this battle: …`,
  `first timid man stepping into a window this battle: …`, `YOU: first window of yours punished this battle - …` (D23).
- The [summary] lines (after the riders'): `impact punish settings (step 54): …`, `impact punish windows: …`, `impact punish proof: …`,
  `impact punish men: …` - or `impact punish: no window this battle could be live (impact off, in shadow or the spike) - nothing measured`.
- Courage's summary: timid spells ended by `an enemy reeling / down close by (step 54)`.

### L6l. Courage and cohesion (step 49)

- Mission start: `[courage] mission start: courage: each AI man on foot in a formation weighs his side against the enemy within 6.0 m …
  - read live; every AI man looked at about every 0.5 s; …` (or `courage OFF (ModEnabled | CourageEnabled) - the game's own bravery; the
  looks still MEASURE …`). A change: `[courage] settings changed mid-mission at … s: …`. Switched off: `[courage] <switch> switched OFF
  mid-mission at … s: N timid men let go at once …`.
- First-time lines: `[courage] first walk back this battle: <man> at … s - his side … (himself … + N mates) vs the enemy … (M men) within
  6.0 m = odds … < required … (base … x tired … x morale … x shock … (N hits in 6.0 s) x fever … x hero …); … m from his formation's centre,
  walking to <the nearest bigger group of his side | his formation's centre> … m away [- in PANIC] [, sprinting … s]`; `[courage] first walk
  back ended at … s after … s - <arrived | among his mates | the ground ahead ends | gave up after 15 s | his timid spell ended>; a → b m from
  his formation's centre; <still timid - holds | brave again>; walked N frames`; `[courage] first PANIC this battle: …`. Verbose (bucket
  `courage`): timid / holding / walks / ends.
- The [summary] lines (D18 reads them): `courage settings`, `courage isolated … ON … OFF …`, `courage distance from the formation's centre
  … ON mean / p90 … OFF mean / p90`, `courage timid: … caused by SHOCK …, by the odds alone …; wobbles …; ends: …; not looked at: …`,
  `courage regroup: …`, `courage flocking and panic: …; compression - … shocked a → b m …, by the odds alone …`, `courage by local odds, in
  contact …`, `courage by local odds, men OUT of their formation …`, `courage toughness … ; behaviour weights: set …, re-applied …, given back
  …, errors …`, `courage cost: …`. Never looked at: `courage: no AI man on foot in a formation was looked at in this mission …`.

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
  `values at … (show #1): yours 1 Infantry 72% ± 8 HP 81% ready 34/40 (40 men, f 0.93) | … - ± is 1.00 std, health on, enemy on, ready count on: yellow at or below 75%, red at or below 50% of the men ready` once per battle (verbose at each later show; `ready count hidden (BraceEnabled off - nobody braces)` when bracing is off).
- The ready count (step 24): in the strip's `values at …` line after the health (`1 Infantry 72% ± 8 HP 81% ready 34/40 (40 men, …)`) and its
  tail (`…, health on, ready count on: yellow at or below 75%, red at or below 50% of the men ready`); the first placement's cell layout
  names it (`…, ready count at +24, 2 px in from the sides (39 px deep; …)`); a column's bottom card at 1080p: `… - 1 cell lifted to the screen's bottom edge`.
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
  [summary] hud: orders strip - ready count (men not bracing, step 24): shown 120x (plain 80, yellow 30, red 10), the fewest ready 12/50 (24%, 1 Infantry); hidden: never
  [summary] hud: ALT markers - ready count (men not bracing, step 24): shown 270x (plain 200, yellow 50, red 20), the fewest ready 9/80 (11%, enemy 1 Infantry); hidden: never
  [summary] hud: ALT bars (movie TraxFeverMarks) - on screen 32.5 s of 340.2 s (10%); layer built 14x, removed 14x (markers hidden or nothing to draw 13, mission end 1); …; errors 0
  [summary] hud: ALT bars - shown 14x, on screen 32.5 s over 1950 frames; men marked a frame: mean 180.3, max 200; Athletics bars a frame: mean 180.3, max 200 (summed over frames: green 150000, blue 90000, yellow 60000, orange 30000, red 21600; wounded 40000); fever bars a frame: mean 41.3, max 118; most men looked at in a frame 600 (yours and allies 300, enemy 300), most in a fever 160; frames at the cap 1800 (FeverMarkMax 200 at the first - the nearest drawn); men skipped, summed over frames: behind the camera 9120, closer than 1.5 m 12, off the screen 3400, beyond the cap 400000; cost 70 µs a frame (most 300 µs); errors 0
  ```
  (step 44: the fever marks' two lines - D14; since step 46 the view is "ALT bars" and the line counts both bars - D15. The numbers above are made up to show the shape.)
  The two ready count lines (step 24) prove: the line was drawn (*shown* > 0) and moved through the colours in a
  brace-heavy battle (yellow and red > 0), *the fewest ready* matches what you saw; *hidden …* names why it was not
  drawn (`BraceEnabled off - nobody braces`, `ShowReadyCount off`) - `never shown` in a battle with bracing on = tell Claude.
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

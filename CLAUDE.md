# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

**Trax Combat Enhancements** (working title) — a combat mod for *Mount & Blade II:
Bannerlord* v1.4.8 that makes fights a bit more fun: every landed hit rolls ±50% damage and then lands 25% lighter (step 25, `DamageReductionPercent`, Anton:
slower fights - AFTER the roll, on people, horses and shields, whatever the roll's switches; falls and objects never;
Core `DamageReduction`),
and every fighter has an **Athletics** bar — his stamina, as big as his Athletics skill —
that blows drain and rest refills. The top quarter of his own bar is full strength; below
it his damage upside, attack RATE and run speed fall, down to one attack in five when empty -
since step 13 PAUSE ONLY: the animations play at full speed (step 20b: a straight line by f down to
85% at empty - Anton's tuning; the run floor 0.6) and a no-attack timer of
D × (1/m − 1) follows each attack (D at FULL animation speed, so the pause in seconds never grows with
the slower swing; the player's input gated; the AI's attack bits taken out of its
own input - guard really up - since step 16, NoAttack before; the player sees an Attack recovery bar
above his Athletics bar); wounds cap the bar; tired AI fighters step back out of the press after a
swing (step 5d) - since step 16 a BACKPEDAL through the AI's own input, facing the enemy, and the
pause survives it (one per-man `AgentComponent` on `OnAIInputSet` does both; A/B switches keep the old ways).
Heroes and party leaders pay less per blow (and big-skill heroes have
big bars), so the game leans hero-centred. Kicks and shield bashes cost a little too (step 18:
`CostPerKickOrBash` 3 × the same multipliers, once each, never an attack pause). DEFENDING costs too (step 22):
every melee blow a fighter BLOCKS costs HIM - `CostPerShieldBlock` 1 (shield, right side), `CostPerWrongSideShieldBlock`
5 (2 since step 43), `CostPerWeaponParry` 2 - × the same multipliers, once per blocked blow (OnMeleeHit's collision data, Core `BlockMath` /
`BlockTracker`); since step 29 only a WRONG-side shield block restarts the refill delay (`OnlyWrongSideBlocksStopRefill`,
Anton: a skilful defender recovers on the defence - a clean block or parry costs, the refill runs on underneath; off =
every paid block, step 22); never an attack pause or a step back; missiles into a shield
free. Step 22 also shipped the refill a straight line again (`RegenRateNearFullPercent` 100) and the run floor 0.3
(config format 5 migrates both once). A hideout's boss
fight is a fresh start for the player's side (step 19, `HideoutBossFightRefill`: when the game's
"Win the Duel" / "Win the Fight" objective appears, his side refills to its wound cap and every pause /
step back on them ends; in a duel his men stand aside, so only he refills). BATTLE PACE (step 21,
Anton: slower, more defensive fights): the AI's pause after each attack gets a share on top of tiredness
by the attack's CLASS, read at its release from what he holds (`IWeaponFacts`) - shield infantry swing
`ShieldInfantrySwingsLessPercent` (30) % less, other foot melee `FootMeleeSwingsLessPercent` (15): pause =
(T + q (D + G)) / (1 − q), G = `AiMeleeGapSeconds` (1.0, the AI's own gap measured in Anton's logs - a pause
adds to it), so fresh men are held too; bowmen +`ExtraPauseAfterBowShotSeconds` (2.0 s), crossbowmen
+`ExtraPauseAfterCrossbowShotSeconds` (2.5 s); riders' melee, thrown weapons, slings and the player: none;
the summary's "battle pace" lines give each class's attacks a minute per man. BRACE BY ORDERS (step 23,
Anton: "not swing but only defend" below a floor set by the orders): each AI man's bar (points ÷ his FULL pool)
is compared about every 0.25 s (staggered) with his formation's movement-order floor - charge
`BraceFloorChargePercent` (20), advance `BraceFloorAdvancePercent` (40; an AI formation's Move under an advance
behaviour counts), the rest `BraceFloorHoldPercent` (60), NO formation (tournaments, arenas, duels - step 28)
`BraceFloorNoFormationPercent` (20); each man's floor ± his own roll of `BraceFloorSpreadPercent` (10, once per man and
battle, its own dice - step 28); at or below it he BRACES (a fourth wish on step 16's
input component: melee attack bits out, guard up; ranged let through) until floor + `BraceRecoverPercent` (20)
± his own roll of `BraceRecoverSpreadPercent` (5, once per man and battle), the target capped by his wounds (the
band slides down under the cap - never stuck); the shield taken out (`BraceWieldShield`: TryToWieldWeaponInSlot, a
one-hander first) and held up (`BraceRaiseShield`); the summary's "brace" lines answer "do lines turtle". The Athletics
bar is shown for the player (step 6)
and — averaged, with a ± spread and the men's health — in a strip under each formation card of
the orders menu (step 9) and, while you hold ALT (or the orders menu is open), under each of the
game's formation markers, the enemy's too (step 20: vanilla's markers read live); both also count
each formation's men READY - not bracing - as "ready 34/50", plain / yellow / red (step 24, `ShowReadyCount`;
hidden while bracing is off). A tired AI man's block DIRECTION is partly a guess (step 30, Anton 2026-10-12:
`TiredBlockRandomPercent` 100 - w = % × (1 − f), the stat decorator raises the game's own per-agent
`AiRandomizedDefendDirectionChance` to own + (1 − own) × w; never the player; Core `TiredBlock`). SWING STACK (step 32, `SwingStackEnabled`): the pause runs only once a fighter's
stored swings - max(`SwingStackMinimum` 2, round(his CURRENT points / `SwingStackPointsPerSwing` 20)) - are spent; one comes
back per pause while he is not attacking (an AI melee man's own ~1 s gap after a swing from the stack excluded), so he can
burst; kicks / bashes spend one too and are gated at 0 (`KicksUseSwings` - your kick key, a held AI man's input); the
recovery bar reads "Swings 4/8". SPRINT (step 33, `SprintEnabled`): a store of seconds = current points / `SprintPointsPerSecond`
20, never below `SprintMinSeconds` 2 (step 35), refilled in 7 s (slower moving or tired), 20% to start; hold `SprintKey` (a key
NAME, LeftControl) or an AI formation's NEW order (step 37, Anton: one number per order kind, the RESERVE he keeps, % of
his FULL store - he sprints everything above it, stored − keep: charge keeps 0, advance 50, move 80, so men moved or
advanced into position keep their sprint for the real charge; no 20% gate for the AI, 0.2 s minimum) → step 35: `SprintMaxMultiplier` (2.0) × his
FRESH run for the first 30% of it, whatever his Athletics, then a line to 1.1 × his CURRENT run (the decorator writes the
sprint's run factor on `MaxSpeedMultiplier` in place of the tiredness multiplier, and caps `TopSpeedReachDuration` at
`SprintTopSpeedReachSeconds` 1 s while sprinting - the game's ~2.3-2.8 s), no Athletics refill meanwhile; on foot only; the
sprint bar above the recovery bar. SHIELD WALLS HOLD THEIR SLOTS (step 41, `ShieldWallHoldsSlots`, Anton 2026-10-17: men stepped
2-3 m out of a waiting wall to swing and walked back): every AI man on foot in a shield wall / square / circle, under any
order but a charge or a retreat (not detached, fleeing, or far from his slot AND behind the line; never you, riders, a
siege's attackers, naval), gets the engine's "keep with the formation instead of moving to the agent" flag - vanilla's own
`Agent.SetFormationIntegrityData` call rewritten with it TRUE from `OnPreMissionTick`, right after each of vanilla's ~0.5 s
updates (his private update timer read once by reflection; RESEARCH §O) - RBM's idea without Harmony; off = vanilla's value
written back at once; the summary's "wall" lines measure how far men stand AHEAD of their slots, ON vs OFF. A SMALL STEP OUT
(step 42, `ShieldWallStepOutMetres` L 1.0 m, Anton's playtest of 41: held men "looked many times like they were about to reach
to hit but stopped"): a LEASH per held man - an enemy (the AI's target, else his immediate enemy) within L + 2 m in front of his
SLOT frees him (vanilla's own value - he closes and swings) until he stands L out of his slot or the enemy is gone, then he is
held (TRUE) and walks back; a new step only once back within min(0.3 m, L/2) of his slot; "out" = the facing for a shield wall,
away from the centre for a square / circle; a man out is looked at every 0.1 s; L 0 = step 41 exactly; the summary's "wall step
out" line (steps, a man's rate, how far, how each ended, the overshoot past L). AGGRESSION FOLLOWS ATHLETICS AND THE FEVER OF A CHARGE (step 43,
Anton 2026-10-17: "a man at work doesn't hold a pace until he drops; the more tired he is, the less he swings" - and "when the
general says NOW!, everybody surges ... the surge fades"): every AI man ON FOOT's two melee attack-decision values (the game's
`AIAttackOnDecideChance` - 0.144 charge / 0.096 / 0.05 wall - and `AIAttackOnParryChance`, assigned with `=` by the base model)
× a share sliding with his bar (points ÷ FULL pool) from `AiAggressionFreshPercent` 60 to `AiAggressionExhaustedPercent` 20, in
0.05 steps riding the speeds' re-target (the stat decorator; never you, never riders, never ranged; × 5e's m if that switch is
on) - a tired man has two brakes, fewer decisions and the pause; a NEW charge / advance order (the sprint's order poll) arms a
FEVER of his CURRENT points ÷ `FeverPointsPerSecond` 10 s at an ABSOLUTE `FeverAggressionPercent` 80 (never below his tired
share), its clock from his first enemy within `FeverContactMetres` 5 m, burning × 1/0.7 with a shield in hand (read live),
fading to his CURRENT tired share; a hold / move / retreat ends it; feverish men roll every 1 s (50% × the fever left) for a
sprint at an enemy 3-12 m off, above their order's reserve; no formation = no fever; the [fever] summary proves it - AI melee
attacks per man-minute in contact by bar band, without / in a fever / in no formation. Swings are cheaper: `CostPerBlow` 3 and
`CostPerWrongSideShieldBlock` 2 in defaults.json (config format 8 moves a file's old 10 / 5 once). THE FEVER MARK UNDER ALT
(step 44, `ShowFeverMarks`, Anton 2026-10-17: "see which soldiers are in FEVER" where the game shows its ALT icons): exactly while
vanilla shows its formation markers (step 20's flag - ALT or the orders menu), a small bar of OURS above each AI man in a fever
(armed / burning; vanilla's own per-man icon mesh is never touched - private, rewritten on every highlight): bright orange and full
while armed, the fill = the fever left fading through yellow, gone when it ends; the feet + `FeverMarkHeightMetres` 2.5 m through the
combat camera every frame, vanilla's icon fade (1.5 → 8 m), smaller far away; yours always, the enemy's behind `FeverMarksShowEnemy`;
at most `FeverMarkMax` 200 (the nearest on the screen) from a reused pool - 0.03 ms a frame, 0 bytes for 400 men in a fever (smoke). AN ATHLETICS
BAR ABOVE EVERY MAN UNDER ALT (step 46, `ShowAthleticsMarks`, Anton 2026-10-17: "how tired they are in a bar above that fever bar ... green
that gets to red"): the same view (the log's "ALT bars") draws a stack per man - his Athletics bar (fill = points ÷ FULL pool, the wound cap
dark red, BarMath's band colours) above his fever bar, the fever bar's place kept so it never jumps; every tracked man but you (riders 1 m
higher, heroes too), the enemy's behind `AthleticsMarksShowEnemy`; ONE cap (`FeverMarkMax` = men marked); the pick sorts and projects
nearest first until the cap is full (an off-screen man never takes a slot) - 0.07 ms a frame, 0 bytes, 200 projections for 1000 men all
on the screen (smoke). `CostPerBlow` 7 in defaults.json since step 46 (Anton's playtest; config format 9 moves a format-8 file's 3 once). THE IMPACT
SYSTEM (docs/IMPACT.md - staggers, dropped shields, knockdowns by both men's Athletics; Anton approved it 2026-10-17, steps 47-53) starts with
step 47's SPIKE (Advanced `ImpactSpike`, off - a TEST TOOL): fixed loud values prove the six engine levers in a real battle - a kick's
KnockDown decision, the block stun x3 with the cap 2 s, a reaction at speed 0.5, a forced `act_stagger_backward`, a queued shield drop,
a knockdown blow of our own - each against its own control in the same battle, and the `[summary] impact spike` lines read WORKS / FAILS
/ UNSEEN per lever with what it means for steps 48-53 (PLAYTEST D16). The levers are the REUSABLE seams of 48-53: `ImpactHooks` (the
damage decorator's KnockDown decision + block stun deciders - the stun on worker threads -, the stun cap, our blow's mark) and
`IImpactBody` (SlowReaction, ForceStagger, DropShield, OwnKnockdownBlow - never a rider or a horse). The bar for the
fighter you look at is LATER (DESIGN §3; its settings wait in DESIGN's "Planned parameters"). Words:
the pool/bar/points are "Athletics", the character-screen skill is "the Athletics skill" (it
used to be called "endurance"), the "peak line" is the white mark at the top quarter of the bar,
"the pause" is the no-attack timer after an attack (step 13).

**The full spec is `docs/DESIGN.md`. Read it before any work.** Released on **Steam Workshop
only** (no Nexus).

## Who does what

Same team and spirit as the sibling mods (`..\TrainingBattlesMod`, `..\ImmersiveAI`). Anton
is the **product owner** — dreams, directs, playtests. Claude is the **developer**. Anton is
an AI engineer but newer to modding, so explain Bannerlord mechanics when they surface. We
work as friends: have opinions, push back, propose.

## How this project is built — the manager protocol (Anton's rule, 2026-09-27)

The work is a numbered **BUILD ORDER** in `TASKS_TODO.md`. It exists so that a session can
die at any moment (tokens run out) and the next one loses nothing.

- **The main session is the manager.** It keeps its own context small: it picks the next
  step, briefs ONE agent to do it, checks the result (build, tests, the agent's report),
  and moves on. It does not read the game's source itself when an agent can.
- **One step at a time, one agent at a time.** Never fan out parallel agents. Quality over
  speed.
- **A step is done only when**: it builds, `dotnet test` is green, its `TASKS_DONE.md` entry
  is written, its `TASKS_TODO.md` line is checked, `AI_NOTES.md` holds any new findings,
  and it is **committed and pushed**. Nothing lives only in a conversation.
- **Mark the step `[~]` in TASKS_TODO before starting it** (and commit that), so a crashed
  session shows what was in flight.
- **Resuming**: read `TASKS_TODO.md` → the first `[~]` or `[ ]` step in BUILD ORDER →
  `git status` / `git log -5` for half-done work → read the step's `AI_NOTES.md` section →
  continue or restart that step.
- An agent's brief must stand alone: point it at this file, `docs/DESIGN.md`, the step's
  `AI_NOTES.md` section and `docs/RESEARCH.md`; say exactly what "done" means (the list above).

## The files

- **TASKS_TODO.md** — Anton's board: short lines only, readable at a glance. BUILD ORDER
  (the steps), PLAYTEST, BUGS, NOT DECIDED. At most a "(see AI_NOTES)" tag on a line.
- **AI_NOTES.md** — Claude's detail companion: one section per step / idea — designs, APIs
  found, gotchas. Read the step's section before picking up its line.
- **TASKS_DONE.md** — the changelog of WORK: one dense `- [x]` entry per finished step, what
  was built and WHY, ending with a `(YYYY.MM.DD HH.MM.SS)` timestamp. Write it so the next
  session starts warm.
- **docs/DESIGN.md** — the behavior contract + the parameter table.
- **docs/RESEARCH.md** — verified game-API findings for v1.4.8 (built in step 2).

## Hard requirements

- **Every number is a parameter.** All values in DESIGN's parameter table live in the config
  file (created on first run, a plain-words explanation beside each value) AND in MCM.
  A new number means a new row in DESIGN's table, same commit.
- **MCM is truly optional.** The sibling mod learned this the expensive way (its MCM
  settings class has an MCM base type, so the mod will not load without MCM): the game calls
  `Assembly.GetTypes()` on our DLL at startup, and ANY type whose base type, interface or
  field type comes from a missing assembly makes the whole mod fail to load. MCM types may
  appear in METHOD BODIES ONLY (e.g. MCM's fluent builder), or in a satellite assembly loaded
  by hand. Same rule for any other optional dependency. Trap inside the trap (step 3): a
  LAMBDA whose parameter is an MCM type is cached by the compiler in a static field typed
  `Func<McmType,…>` — an MCM field after all. `McmBridge` hands MCM `Action<object>` instance
  methods instead; `tools/AssemblyGuard` (run by every deploy) catches any slip.
- **Decorate game models, never subclass `Default*`/`Sandbox*` models.** `AddModel` replaces
  by base type; a subclass silently drops War Sails' and other mods' versions. Extend the
  abstract model and delegate to the previously registered one (sibling lesson). If a model
  cannot carry what we need, Harmony is acceptable — say why in AI_NOTES.
- **Performance**: battles have 500–1000 agents. No per-agent allocations per tick; poll
  what needs polling at a modest rate; UI updates throttled.
- **Hot-swappable settings (Anton, 2026-09-27): changing ANY parameter in MCM must never
  need a game restart.** Target: live — the next hit / blow / HUD refresh uses the new
  value, even mid-battle (MCM opens from the Escape menu). Code reads parameters from the
  one shared config object AT USE TIME, never copies them into fields at mission start;
  anything that must be rebuilt (e.g. a HUD toggled on, an agent's cached speed penalty)
  listens for a settings-changed event and rebuilds itself. Where live truly is not
  possible, the fallback is "applies from the next battle" — never "restart the game" —
  and that parameter's MCM hint and config-file comment say so. Hand edits of the config
  file are re-read at every mission start (and on load), so they need no restart either.
  Every change is logged (`[config] X: old → new (source: MCM|file)`).
- **The master switch comes first (Anton, 2026-09-27).** Every feature gates on
  `TraxSettings.Shared.ModEnabled` FIRST, read live: off = that feature is pure vanilla at once
  (it lifts whatever it applied - penalties, bars, step-backs), while the mission logging keeps
  running so an ON battle and an OFF battle compare (DESIGN §4). Back on = a fresh start
  (everyone's Athletics full). New code: check it before anything else, and add the feature's
  "off" behaviour to the smoke's master-switch step.
- **Defaults live in `defaults.json`, nowhere else (step 5b, DESIGN §2c).** The schema has no
  default values; a new setting needs `"Key": value` in defaults.json, then
  `dotnet run --project tools/DefaultsTool -- refresh`. DESIGN's Default column is the INITIAL
  value only - never "fix" it to match a tuned defaults.json.
- **Save-safe**: the mod lives inside missions. It must add nothing to the campaign save, so
  it can be enabled or removed mid-campaign.
- **Logging built for one big playtest at the end (Anton, 2026-09-27).** Anton tests
  everything at once when the build is finished, so the log must let us troubleshoot any
  feature WITHOUT a second run. One rolling log file (`trax_combat.log`) beside the config
  file, capped at `LogMaxMegabytes` (8); a trim cuts ONLY the oldest verbose lines (marked `~`
  before the tag) and keeps every other line - summaries, first-time lines, settings, errors
  (step 10b, Core `LogTrim`). Timestamped lines tagged by area (`[config]`, `[mcm]`, `[mission]`,
  `[damage]`, `[athletics]`, `[speed]`, `[rate]`, `[stepback]`, `[brace]`, `[guard]`, `[sprint]`, `[wall]`, `[fever]`, `[impact]`, `[hud]`, `[error]`). Always logged: mod/game version
  at load, every parameter value on load and on change, each mission start/end (type,
  scene, agent counts), which behaviors/views attached, and every caught exception with its
  stack. Per-battle SUMMARY at mission end (damage rolls: count, min/avg/max factor;
  Athletics: blows charged, exhaustions entered/left, heroes' lowest Athletics, formation
  averages). Chatty per-event lines (each roll, each blow, each regen tick) only when
  `VerboseLogging` is on — and even then rate-limited so a 1000-agent battle cannot flood
  the file; a hot path asks `TraxLog.VerboseWants(bucket)` BEFORE building its line (R8). Every
  game hook is wrapped in try/catch that logs `[error]` and fails SAFE (the
  vanilla behavior), so a bug in the mod never crashes a battle.
- **docs/PLAYTEST.md grows with every step**: what Anton should try, what he should see, and
  which log lines prove it worked. It is the script for the final test session.
- **Commit as each good piece lands** — not only at step end. Small, working commits.

## Layout (real since step 3, 2026-09-27 — keep it true)

```
TraxCombatEnhancements.sln    Core + Module + tests (the tools build on their own)
Directory.Build.props         C# 10, nullable, GameFolder, McmBinFolder; *.user override imported;
                              TraxModVersion read from module\SubModule.xml <Version> (step 11) - both
                              DLLs' <Version> is $(TraxModVersion); an empty one fails the build
defaults.json                 THE ONE TRUTH for every default value (DESIGN §2c): every setting,
                              its value, its explanation + range as // lines. Anton tunes it and
                              pushes; embedded in TraxCombat.Core.dll at build. After a schema
                              wording/range/order change: dotnet run --project tools/DefaultsTool -- refresh
docs/HOW-IT-WORKS.md          step 31, THE PLAYER GUIDE (linked from the README and the Workshop page): every mechanic
                              by topic in TABLES computed from defaults.json (pool / attacks per bar, tiredness by bar
                              level, refill, block costs + "does a parrying man climb", tired block guesses, brace
                              floors + a worked example, step back, pace, damage, HUD colours, tactics) - setting
                              names in small print. A tuned default changes its tables: recompute them
docs/BATTLE_PACING.md         step 15's research (how RBM paces battles, our levers) - developer reading
docs/REVIEW.md                the code reviews: step 10a (R1-R24) and step 17's second one over steps 12-16
                              (R25-R36) - every finding with its severity, place, scenario and status (fixed
                              in which commit / not a bug / deferred / For Anton)
docs/PLAYTEST.md              THE script of Anton's one playtest session (step 10b: one ordered run,
                              parts A-G, ~2 h; appendix L1-L8 = every log line and summary block)
src/TraxCombat.Core/          netstandard2.0 — pure logic, no game refs, unit-tested:
  ParamDef.cs                 one setting: key, type, range, group, label, plain-words
                              description, apply timing (Live / NextBattle); Normalize, Format;
                              its Default comes from defaults.json (DefaultsFile), never code; step 33: ParamType.Key +
                              KeyNames (a key by NAME, stored as its index; FormatJson writes "LeftControl"; the
                              file / defaults.json read the name, MCM a text box)
  SettingsSchema.cs           EVERY setting of DESIGN's table (151 - step 47's ImpactSpike in Advanced, before VerboseLogging), in file + MCM order, 14 groups
                              ("Master switch" first, "Damage" second - step 25 renamed it from
                              "Damage randomness", "Battle pace (AI)" - step 21 - after the step back (step 41's
                              ShieldWallHoldsSlots and step 42's ShieldWallStepOutMetres at its end),
                              "Brace by orders (AI)" - step 23 - after it, "Sprint" - step 33 - after that, "Aggression and
                              fever (AI)" - step 43 - after the sprint,
                              "Hold ALT: formation markers, Athletics and fever bars" - step 20, renamed by steps 44 and 46 (was
                              "Formation markers (hold ALT)"; + ShowFeverMarks, FeverMarksShowEnemy, step 46's ShowAthleticsMarks,
                              AthleticsMarksShowEnemy, then FeverMarkMax; Advanced + AthleticsMarkWidth / Height / Gap) - before
                              "Advanced" last; step 10b's one vocabulary and
                              units in its header comment) — the one place a setting is declared
                              (a test parses DESIGN.md: keys + types); NO default values. The LATER
                              features' settings are NOT here (DESIGN "Planned parameters")
  DefaultsFile.cs             defaults.json: the embedded copy → ParamDef.Default (fail safe:
                              problems listed, never thrown), strict Check (keys both ways,
                              JSON types, ranges, comment layout), Write (the repo file and the
                              MCM export alike), ResolveAll (tests); UseValuesForTests
  ModSwitchLog.cs             the master switch over one mission: toggles with times, on-share,
                              "mod ON" / "mod OFF" / "mod was on for N% …" for the summary header
  TraxSettings.cs             THE live settings object (TraxSettings.Shared), read at use time:
                              typed properties, Set(key, value, source), Version, Changed event
  ConfigFile.cs               config.json TEXT: commented writer (header incl. how to revert +
                              // above each key; AppendSettings shared with defaults.json),
                              tolerant reader (comments, trailing commas, casing, "0,75"), Apply;
                              FormatVersion 9 + Migrate (step 46: a format-8 file's CostPerBlow 3 → 7 once, a tuned value
                              kept and noted - KeptTuned; step 13: a format-1 file's old defaults -
                              AttackRateAiDecisions true, PlayerBarOffsetBottom 54 - get the new ones once;
                              step 14: a format-2 file's MinMoveSpeedMultiplier 0.3 → the default once;
                              step 20b: a format-3 file's 0.7 → 0.6 and a format 2-3 file's
                              AttackAnimationMinPercent 100 → 85 once; step 22: a format 3-4 file's
                              RegenRateNearFullPercent 50 → 100 and a format-4 file's MinMoveSpeedMultiplier
                              0.6 → 0.3 once; step 37: a format-6 file's SprintAiAdvanceKeepPercent 0 → 50 and
                              SprintAiMoveKeepPercent 70 → 80 once; step 43: any older file's CostPerBlow 10 → 3 (today's default - 7 since step 46) and
                              CostPerWrongSideShieldBlock 5 → 2 once; any format: DropRetiredSprintKeys - step 33's two
                              SprintAi*SharePercent and step 36's three SprintAi*UsePercent leave Unknown, one note each;
                              Migrate(read, defaultOf) = the
                              seam the tests hand defaults.json's values through - they run on DESIGN's)
  ConfigMerge.cs              THE FILE-REWRITE RULE (MCM wins for what it touched, the disk for
                              the rest) + EditTracker (what MCM changed since the last write)
  RateLimiter.cs              per-tag token bucket for chatty log lines, counts what it drops;
                              Peek (R8: ask before building a line - a "no" is counted as dropped)
  LogTrim.cs                  step 10b, the log's trim rule (R7): entries (a stamped line + the
                              unstamped lines under it), KEPT = every non-verbose line + the [load]
                              [compat] [config] [mcm] [mission] [summary] [error] tags, CUT = the oldest
                              verbose (~) lines at one point in time; last resort: the oldest kept;
                              one note at the top; Utf8Bytes
  McmPlan.cs                  step 12, when the MCM bridge tries and when it stops: ModuleEnabled (MCM's module
                              Bannerlord.MBOptionScreen in the enabled list; unknown = try), Decide (no DLL →
                              NotLoaded, DLL but module off → ModuleNotEnabled, one line), GiveUp after the first
                              + MaxRetries (30, a documented constant) "not ready"s, the three [mcm] lines
  SingleCopy.cs               step 11, ONE copy runs: TryClaim(token) on an AppDomain data slot (the first
                              SubModule instance to ask runs, a later one is refused and counted - works
                              for one shared assembly and for two), Refused, CopiesIn (the release id
                              and its dotted variants), IdFromManifest, the [compat] lines + the message
  RandomSource.cs             IRandomSource (injectable dice); ThreadSafeRandom ([ThreadStatic]
                              Random per thread, the game's); SeededRandom (tests, smoke)
  DamageRoll.cs               DESIGN §1 pure: HitFacts, DamageRules (live from TraxSettings),
                              Decide = the skip rules (ModOff last), factor U[1-p, 1+p×upside),
                              Upside(rules, attacker f) (DESIGN §2), game rounding, 0 stays 0,
                              positive never below 1; RollOutcome (Upside, Ceiling);
                              DamageCategory, DamageSkipReason; step 25: DamageRules.ReductionPercent
  DamageReduction.cs          step 25 pure (AI_NOTES "Step 25"): ReductionSkipReason (objects, no victim, fall,
                              0 hit, the setting at 0, ModOff LAST - counted like the roll's), DamageReduction -
                              Decide (independent of the roll's switches), Apply = x (1 − r/100), 0 stays 0,
                              positive never below 1, clamped 0-90 (MaxPercent), Reduce → ReductionOutcome (the
                              game's / after the roll / landed), Example ("an average 100 hit lands 75")
  DamageStats.cs              per-mission roll stats (kinds, min/avg/max, before → after, avg per
                              hit, dice histogram, skips by reason, mod-OFF hits unrolled, the
                              upside by the attacker's f + rolls above their top, errors per
                              site, thread) + the [summary] text; thread-safe; step 25: the reduction -
                              hits by kind, the game's → after the roll → landed, the percents used, skips by
                              reason + the [summary] "damage reduction" line
  Athletics.cs                DESIGN §2 pure (Athletics v2, step 5c): AthleticsRules (live;
                              Enabled = ModEnabled && AthleticsEnabled, OffBecause, the floors),
                              Fighter (state as a FRACTION of the FULL pool, AthleticsSkill,
                              Health, the three applied multipliers), AthleticsMath - ONE function
                              per rule (PoolPoints = max(floor, per-skill × skill), UsableFraction
                              = the health cap, PeakShare = f, Attack/Run/MountSpeedMultiplier =
                              floor + (1 − floor) × f, DamageUpside, BlowCostPoints in points,
                              step 18: KickOrBashCostPoints + ChargeKickOrBash (Fighter.KicksAndBashes,
                              never Blows; cost 0 = nothing at all),
                              RegenRateMultiplier by effort, SpeedUpdateNeeded (0.05 step), PeakBin;
                              step 14's refill curve: RegenCurve = 1 − (1 − k) x, RegenRateAtEmpty r0 =
                              ln(1/k) / ((1 − k) T), RefillFrom / RefillSeconds - exact per step;
                              step 19: FreshStart - the refill to the top the wounds allow + FreshStartOutcome;
                              step 22: BlockCostSetting / BlockCostPoints by BlockKind + ChargeBlock
                              (Fighter.BlocksPaid, never Blows; cost 0 = nothing at all), the three
                              CostPer*Block/Parry rules; step 29: BlockRestartsRefill - only a wrong-side
                              block restarts the delay while OnlyWrongSideBlocksStopRefill is on, else
                              Drain leaves LastBlowTime / Regenerating alone; BlowOutcome.RestartedDelay)
                              + Charge / ApplyHealth / Regen / Read; BlowKind, BlowOutcome,
                              RegenOutcome, AthleticsReading (HUD snapshot incl. f, usable pool,
                              BelowFull = below the top it can refill to - step 12)
  KickBash.cs                 step 18: KickBashKind, KickBashTracker - ONE decision per kick / shield bash:
                              Observe(channel, action) = the rising edge on channel 1 or 0 (OR-ed: one
                              kick on both = one), Hit = the fallback when no channel shows one (the
                              poll's late sight within SameActionSeconds 1.0 not charged again); the
                              engine's action codes (the smoke checks them)
  Block.cs                    step 22: BlockKind (none / shield right side / shield WRONG side / weapon parry),
                              BlockMath - KindOf (the engine's AttackBlockedWithShield, CorrectSideShieldBlock,
                              CollisionResult; missiles, kicks / bashes, horse charges, the shield on the back
                              = None), the result codes (the smoke checks them), Name, SettingKey;
                              BlockTracker (a struct on the defender: the same attacker's same swing within
                              SameBlowSeconds 1.0 = the same blow - one charge per blocked blow)
  HideoutBossFight.cs         step 19: the hideout boss fight's facts - BossObjectiveId, the duel / battle text
                              ids (KindOf from TextObject.Value), the controllers by name, BossFightKind,
                              HideoutSide (Player / Boss / Aside / Gone), RefillOffBecause (ModEnabled first);
                              HideoutBossFightStats: the intro, the fight's start (once), the refill counts, what
                              it released, the boss side's freshness + the [athletics] line and the [summary]
                              line ("intro played, fight never seen: tell Claude")
  StepBack.cs                 DESIGN §2 step back pure (step 5d): StepBackRules (live; Enabled =
                              ModEnabled && AthleticsEnabled && StepBackEnabled, Describe),
                              StepBackMath (Chance = max × (1 − f), Roll, AwayFrom = the spot,
                              Radians = the game's facing convention, FacingCosine/Bin,
                              SpeedAway/MotionBin, LevelEnough, TimeUp read live; plumbing
                              MaxHeightStep 1 m, MaxStartsPerTick 20), StepBackNotRolled /
                              StepBackRefusal / StepBackEnd (every reason the summary names; step 16:
                              Arrived, EdgeAhead); step 16: StepBackRules.Backpedal (StepBackBackpedal)
  StepBackStats.cs            per-mission step-back counters + the 8 [summary] lines (rolls by
                              f bin, starts, ends by reason, moves, facing, guard, release checks);
                              step 16: starts by technique + TechniqueText (MIXED when switched), every
                              0.25 s facing + "back turned at ANY sample", arrived, m/s, input releases
  AiInput.cs                  step 16 pure (AI_NOTES "Step 16"): AiInputMath - HoldAttacks (only the attack
                              bits out; a guard DefendDown raised when he wanted to attack, always in a
                              ready = cancel, never release; his own guard kept), Backpedal (his move bits
                              out), BackpedalVector (the away line in his own frame = Mat3.TransformToLocal),
                              Covered / Arrived, Direction, LaterEnd, DeferredStillWorth, the engine's bit
                              values (the smoke checks them) + plumbing (full stick, 0.6 m / 0.25 s ground
                              check, 0.25 s samples); InputEdit
  AiHoldStats.cs              step 16 per-mission: the GUARD by state (held / stepping back / both / everyone
                              else tired / fresh), the hook's counts (men hooked, callback already on / on /
                              off, calls per second, edits, never-called holds, errors), overlaps and Legacy
                              deferrals, switches - the 4 [summary] "AI holds" lines
  AttackRate.cs               DESIGN §2 attack RATE pure (step 5e): AttackKind, AttackPhase (wind-up,
                              held, release, clean release, recoil, reload, pause), the AI timer's
                              reasons (PaceNotHeld - step 16: CoveredByStepBack / PaceRefusal / PaceEnd /
                              PaceRelease), AttackRateRules (live; AiDecisionsOn / PaceOn / PlayerTimerOn,
                              the animation floor, the master switch first; step 16: PaceByInput,
                              RaiseGuard, PaceTechnique), AttackRateMath (ScaleChance x m / ScaleWait
                              ÷ m, CycleCap, TargetCycle = fresh ÷ m, Verdict ±15%; plumbing constants)
  AttackTimer.cs              step 13 PAUSE ONLY pure: AttackTimerMath (Pause = D x (1/m − 1), Worth
                              ≥ 0.1 s, step 20b: AnimationMultiplier(f, A) = A + (1 − A) f (was max(m, A)),
                              PeakShareOfAttack / AnimationForAttack = f read off the APPLIED m,
                              AtFullSpeed = played × animation (D's unit), CountdownText "1.3 s" (tenths
                              up), FlashOn - 2 pulses, the recovery bar's colours), PlayerAttackTimer
                              (YOUR timer: the hold from the release's start, the countdown, Frame =
                              the input gate's decision - clear / swallowed / flash / ended / held at
                              the end, Release), PlayerGateFrame, PlayerTimerEnd, AttackRecoveryReading
                              (the Attack recovery bar's read: Share, SecondsText, the flash)
  AttackRateStats.cs          per-mission attack rate: melee / ranged x AI / you x f band - the
                              animation asked, every phase, the cycle, m, the target (step 20b: + the
                              slower swing's own time, AnimationExtra), measured ÷ target + verdict; the
                              TIMER rows (D, m, the pause asked, the measured gap to the next attack,
                              after its end, early starts; step 16: the timer's floor D/m and the cycle
                              against it; step 20b: the played attack beside D, the floor = played +
                              the pause); left-out counts (mixed, beyond the
                              cap, cancelled, chained; step 16: cycles with a step back inside are
                              COUNTED and shown apart per band); your timer (presses swallowed,
                              flashes, held-button fires, missed attacks, releases); the AI timer (by
                              input / by NoAttack, kinds, mounted, reasons, ends); the guard by f;
                              the AI-decision recomputes; the [summary] "attack rate" lines; step 21:
                              each timer row's battle-pace share, the peak band in the holds list
  BattlePace.cs               step 21 pure (AI_NOTES "Step 21"): AttackClass (shield infantry, other foot
                              melee, riders, bow, crossbow, thrown and slings), RangedWeaponKind, PacePlan
                              (the tired part + the share), BattlePaceRules (live; SettingText, Describe),
                              BattlePaceMath - Classify / Consistent (the class from what he holds, checked
                              against the attack that ended), Plan (foot melee: (T + q (D + G)) / (1 − q);
                              bows / crossbows: + seconds; the rest: T), ModelCycle, PerMinute, FewerShare,
                              the class names; MaxSwingsLessPercent 90
  BattlePaceStats.cs          step 21 per-mission, per class (AI only): attacks by N men, pauses by reason
                              (the share alone / tired + the share / tired only), no pause, the tired part and
                              the share, the cycle (a minute per man while fighting), the AI's own gap after a
                              pause, the model's cycle at 0 and with the setting (foot melee), the archers'
                              share inside a cycle + the 7 [summary] "battle pace" lines
  SwingStack.cs               step 32 pure (AI_NOTES "Step 32"): SwingStackRules (live; Enabled = ModEnabled &&
                              AthleticsEnabled && SwingStackEnabled, KicksOn, Describe), SwingStackMath (RoundAway - half away
                              from zero, Cap = max(min, round(points / per swing)), Full, FallbackSwingSeconds 0.8, burst bins,
                              CountText "4/8"), SwingStack (one fighter: Stored, Progress, the clock - Advance stops while
                              attacking, a refill < 0.1 s fills at once, StartGrace / EndGrace for the AI's own gap; Spend →
                              FromStack / AfterWaiting / AtEmpty; Clamp, ProgressAt, SecondsToNext, FillUp, the burst Run),
                              SwingStackStats (+ the 5 [summary] "swing stack" lines); AttackTimer.cs: PlayerAttackTimer's
                              pauseOverride / StartCountdown / Retarget, AttackRecoveryReading.WithStack
  Sprint.cs                   step 33 pure (AI_NOTES "Step 33"): SprintOrder (charge / advance / move / none - step 36 split
                              advance and move), SprintEnd, SprintStart (step 36: + ReserveKept), SprintRules (live; Enabled =
                              ModEnabled && AthleticsEnabled && SprintEnabled, AiOn, Describe; step 37: the three AI keep
                              percents - the reserves -, AiOrdersText), SprintMath (Capacity = points / per second - step 35: max(SprintMinSeconds, it),
                              PointsCapacity, OnFloor -, TiredFactor, RefillMultiplier /
                              RefillPerSecond / SecondsToFull - 7 / 14 / 14 / 28, CanStart (20% - yours), Group (step 23's
                              kinds); step 37: AiSprints (charge / advance / move), AiKeep, AiAmount = stored − keep x cap or 0
                              below AiMinSeconds 0.2, AiReserveStops; step 35's RunFactor (SprintMax × the FRESH run, a line to SprintMin × the CURRENT run -
                              Multiplier = a fresh man's), Quantize 0.05 (never below 0.05), TopSpeedReach = min(game, ours),
                              the bar's colours; plumbing MovingMetresPerSecond
                              0.5, MeleeRangeMetres 3, MeleeCheckSeconds / OrderPollSeconds 0.25), SprintStore (one fighter:
                              Stored - full until the first Clamp -, Refill, TryStart, Advance - drains only moving -, Stop,
                              Share; step 35: RunFactor; step 36: TryStartAi - no 20% gate), SprintReading (the bar), SprintStats
                              (+ the 4 [summary] "sprint" lines; step 36: by order kind with its reserve ("keeps N%"), reserve kept;
                              step 35: off-the-floor starts, the acceleration game → ours, the speed check × the fresh run)
  Brace.cs                    step 23 pure (AI_NOTES "Step 23"): BraceOrderKind (the game's movement orders + the
                              AI's advance / tactical charge on a Move), BraceOrder (charge / advance / hold / no formation -
                              step 28), BraceEnd,
                              ShieldStep, HandFacts, BraceBand, BraceRules (live; Enabled = ModEnabled && AthleticsEnabled
                              && BraceEnabled, OffBecause, Describe), BraceMath - Group, Band (floor = min(F, cap − M),
                              target = min(F + M, cap); M = recover ± his roll, ≥ 1 point), Enter / Leave / LeaveReason,
                              RollUnit / Offset / Margin (the per-man spread, rescaled live), step 28: FloorOffset / Floor
                              (his own floor = the order's + his floor roll x BraceFloorSpreadPercent, clamped 0-1; Band takes
                              both rolls), ShieldStepFor; plumbing
                              PollSeconds 0.25, ShieldCheckSeconds 1, MaxWieldCalls 3, LongBraceSeconds 30
  BraceStats.cs               step 23 per-mission: men polled / braced, braces by order and floor, AI fighter-time vs
                              brace time, lengths (5 bins), ends by reason, the margins rolled + length by margin band,
                              the shield (at start, calls, seen, put away, never), the guard while bracing, the hook's
                              frames, attacks while bracing; step 28: by floor incl. no formation, the floor offsets rolled
                              + braces by floor-offset band + the 8 [summary] "brace" lines
  ShieldWall.cs               step 41 pure (AI_NOTES "Step 41", RESEARCH §O): WallArrangement (shield wall / square / circle /
                              column / other), WallVerdict (held, or why not - in GATE ORDER: ModOff, SwitchedOff, Naval,
                              SiegeAttacker, NotAi, Mounted, NoFormation, Detached, Arrangement, ChargeOrder, RetreatOrder,
                              NoOrder, Retreating, FarBehind), WallFacts (the gates + positions: him, his slot, the facing, the
                              centre, the integrity data), WallRules (live: ModEnabled FIRST, ShieldWallHoldsSlots; Describe;
                              step 42: StepOutMetres clamped 0-3, Leash = it while on else 0 - the 2-argument constructor = 0),
                              WallMath - IsDefensive, OrderVerdict, Eligible (no switches, no escape - the samples), Decide,
                              DistanceToSlot, FarFromSlot (CatchUpSpeeds 6 - vanilla's radius), BehindLine, Escapes, Ahead (along
                              any facing); plumbing PollSeconds 0.25, SampleSeconds 0.5, FallbackRewriteSeconds 0.1, ContactMetres
                              5, AheadNear / FarMetres 1 / 2
  ShieldWallStats.cs          step 41 per-mission: WallAheadSamples (> 1 m / > 2 m ahead, mean of the part ahead, the farthest;
                              in contact apart), WallWriteKind (poll / after vanilla / fallback), WallStats (looks by verdict,
                              writes by kind, vanilla's updates seen + interval, releases by reason, the samples ON / OFF, men
                              without a timer, left the field, held at the end, cost) + the 5 [summary] "wall" lines; step 42:
                              WallWriteKind.Leash, WallStats.Leash, the 6th line (the step out)
  ShieldWallLeash.cs          step 42 pure (AI_NOTES "Step 42"): WallLeash (Free / Armed / Returning, the step's start, start
                              out and farthest out - a struct in WallState), LeashMove (Freed, Reined, EnemyGone, LeashOff,
                              BackInSlot), LeashEnd (reached the leash / enemy gone / leash set to 0 / let go / fell / still
                              out), LeashMath - OutDirection / OutOf / Out (the facing; a square / circle: centre → his slot,
                              the facing within MinRadialMetres 0.5), EnemyCloseInFront (in front of the slot's line, within L +
                              StrikeReachMetres 2 of the SLOT, + EnemyHysteresisMetres 0.5 while out), RearmAt = min(BackInSlot
                              0.3, L/2), NeedsEnemy (the native read only when he could step / is out), Step (L ≤ 0 = the hard
                              hold), Describe; plumbing LookSeconds 0.1, OverMetres 0.5, MaxMetres 3; WallLeashStats (steps, men,
                              a man's rate while held, farthest out, ends, the overshoot of the reined, back in slot, time out,
                              in-contact samples past L / L + 0.5) + the [summary] "wall step out" line
  Fever.cs                    step 43 pure (AI_NOTES "Step 43"): FeverRules (live; ModEnabled FIRST, AthleticsEnabled = AggressionOn,
                              Enabled = + FeverEnabled, SprintOn; the percents, the store, contact, shield, the sprint roll; Describe with
                              a charge's values fresh / half / empty / peak), FeverMath (TiredShare = E + (F − E) × bar, AggressionShare -
                              armed max(T, P), burning T + (P − T) × left ÷ full, never below T -, Quantize 0.05 (ShareStep), DecisionFactor
                              (1 for the player / off), ApplyChance (× then clamp 0-1, no floor), Store = points ÷ per second, BurnRate,
                              Group (charge / advance), SprintChance (× left ÷ full, full armed), SprintRoll, share bands; plumbing
                              PollSeconds 0.25, MaxPercent 200), FeverStore (one man: Arm → Armed / Rearmed / KeptLarger / Repeat /
                              NoStore / Off / NotAggressive, StartBurn, Burn - the shield live, SprintDue, Stop), FeverPhase, FeverEnd,
                              FeverArm, FeverSprintRefusal
  FeverStats.cs               step 43 per-mission: fevers armed by order and by you / the AI general, re-arms, contact delay, never met,
                              burns by shield, ends, decision recomputes per AI man-minute and per fever, fever sprints by the fever left
                              + refusals, THE PROOF - AI melee attacks per man-minute in contact by bar band (no fever / in a fever / no
                              formation), the values written by band and in a fever + the 10 [summary] "fever" lines
  TiredBlock.cs               step 30 pure (AI_NOTES "Step 30", RESEARCH §M): TiredBlockRules (live: ModEnabled FIRST,
                              AthleticsEnabled, TiredBlockRandomPercent > 0; Share, OffBecause, Describe), TiredBlockMath -
                              Weight = share × (1 − f) (0 at f 1, off, the player), OwnChance (the game's value clamped 0-1:
                              a final-round tournament fighter's −1.10 → 0), Chance = own + (1 − own) × w (w 0 = the game's
                              value untouched), RightShare; TiredBlockStats (the chance raised by f band, the AI's GUARD by f
                              band - shield right / WRONG side / parry / landed - + the [summary] lines; step 34: the
                              guard split by a shield in hand at the blow - all / WITH / WITHOUT, 4 lines - and the
                              recomputes with a shield);
                              Fighter.BlockRandomWeight = the w applied now (Athletics.cs)
  ReadyCount.cs               step 24 pure (AI_NOTES "Step 24"): ReadyBand (plain / yellow / red), ReadyHidden (ModOff
                              first, AthleticsOff, BraceOff, SwitchedOff, NoMen), ReadyRules (live: ShowReadyCount,
                              Ready*BelowPercent, Describe), ReadyMath - HiddenFor, Band (the most alarming wins, "at
                              or below"), Text "ready 34/50" (FormationAthleticsStats.Ready of Total), PlainHex = the
                              text brushes' own #E8E8E8FF; ReadyCountStats (shown by band, the fewest ready, hidden by
                              reason + the [summary] "ready count" line of each view)
  AthleticsBar.cs             step 6 (and the strip's colours; step 7's LATER target bar would reuse it): BarBand, BarRules (live
                              Bar*BelowPercent), BarMath - Band (green at the peak line, blue just
                              below, yellow/orange/red at or below their % of the line, the most
                              alarming wins, empty always red), Fill / Usable / PeakLine (the
                              shares the prefab's FillBarWidgets draw), DisplayNumbers ("132 / 180",
                              rounded UP - 0 only when empty), the colours (#RRGGBBAA constants)
  HudGate.cs                  THE show/hide rule of every HUD view: HudHide (ModOff FIRST,
                              AthleticsOff, ToggleOff, HideBattleUI, PhotoMode, NotFightMode,
                              OutsideBattlesOff, NoPlayer, NotTracked, OutsideIdle, ViewCondition; +
                              MissionEnd / Failed as removal reasons), HudGateInput (+ HudOutside, step 12:
                              the OUTSIDE-A-BATTLE rule of the player bar - walk-about mode, tracked, a
                              weapon drawn or below full, the 1 s grace Lingers / OutsideLingerSeconds),
                              Decide, ShowReason (HudShow: fight / weapon drawn / refilling / grace),
                              Describe / ShortName for the log
  HudStats.cs                 one HUD view over one mission: time on screen, hidden by reason,
                              builds, removals by reason, refreshes, errors, disabled, movie
                              failure; for bars: time per colour, colour changes, empty, wounded
                              (lowest usable) + the [summary] "hud:" lines; ConditionName (the
                              view's own condition in the summary - "orders menu closed"); step 12:
                              outside a battle - builds by HudShow, seconds on screen, the clause
  OrderStrip.cs               step 9, the orders-menu strip pure: OrderCard / OrderCardFrame (the
                              vanilla cards read in one frame, pixels), StripFormation,
                              OrderStripMath (Match = the drawn set of 8 + slot k vs formation k by
                              the member counts → Aligned / NotYet / Mismatch / Problem; PlaceCell in
                              pixels, lifted at the screen's edge; Signature; Numbers / texts
                              "72% ± 8" "HP 81%"; Band; the slot names), StripLayout (the OrderStrip*
                              settings; step 24: the ready row at OrderStripReadyOffset counts in Top / Depth
                              only while ReadyRules.Shown - 39 px deep, else step 9's 23), StripFallback,
                              OrderStripStats (+ the [summary] strip line); DescribeValues + "ready 34/40"
  AltMarkers.cs               step 20, hold ALT pure: AltMarker (one vanilla marker as read: key = team index x 16 +
                              formation, team type, vanilla's point, WSign, distance, men, its widget's size / alpha /
                              pinned offsets, the stats), MarkerWidget, AltMarkerFrame (<= 64, reused), AltMarkerMath -
                              Pair (widgets to targets: index first, then the exact point), Nominal (a fallback's size),
                              Sight (enemy off / no stats / faded / not laid out / pinned / behind / shown), FullyOnScreen
                              (vanilla's test), Place (the marker's bottom centre + the gap x the UI scale, a 200 px
                              centring box), VanillaAlpha (the prefab's distance fade); AltMarkerLayout (the AltMarker*
                              settings), AltMarkerTechnique / AltMarkerFallback, AltMarkerStats (+ the [summary] line)
  FeverMarks.cs               step 44, the fever mark under ALT pure (AI_NOTES "Step 44"): FeverMarkSkip (NotPlaced, EnemyOff,
                              Behind, TooClose, OffScreen, Capped - in the order asked), FeverMarksHidden (ModOff FIRST,
                              AthleticsOff, FeverOff, SwitchedOff), FeverMark (one man: side, armed / share, the world point,
                              distance, in front, the screen point), FeverMarkFrame (reused; arrays double only past their
                              most), FeverMarkRules (live: the switches, FeverMarkMax, the layout; Describe), FeverMarkMath -
                              Tint (orange armed / at the burn's start → yellow as it empties, alpha 1 → 0.45, 0 empty), Fade
                              (vanilla's ALT icon: 0 under 1.5 m, a line to 8 m), SizeFactor (1 to 20 m → 0.6 at 60 m), PreSkip,
                              Pick (the nearest N - Array.Sort keys/items, allocation-free), Size, Place, OnScreen, the names;
                              FeverMarkStats (+ the [summary] line: shows, frames, marks mean / max, the cap, skips, cost);
                              step 46: FeverMark = one man's whole mark (+ InFever, Mounted, BarFill / BarUsable / Band,
                              HasBar / HasFever / FeverSlot), FeverMarkFrame.DropLast / Keep, IMarkProjector + PickOnScreen<T>
                              (sorted only when the cap bites, projected NEAREST FIRST until it is full - an off-screen man takes
                              no slot, the rest never projected), FeverMarkStats.NoteBars + the "hud: ALT bars" summary (men
                              marked, Athletics bars by colour + wounded, fever bars, men looked at / in a fever)
  AthleticsMarks.cs           step 46, the Athletics bar above every man under ALT pure (AI_NOTES "Step 46"): AthleticsMarksHidden
                              (ModOff FIRST, AthleticsOff, SwitchedOff), AthleticsMarkRules (live: ShowAthleticsMarks, the enemy's
                              switch, width / height / gap; Describe), MarkBox, AthleticsMarkMath - RiderLiftMetres 1, WoundHex
                              (the player bar's), MarkHeight, Decide (the bars by side - the fever's place kept while fever bars
                              can show for his side - then behind / too close), Read (fill = points ÷ FULL pool, the wound cap,
                              BarMath.Band), Layout (the stack: the Athletics bar, the gap, the fever bar; only the fever bar =
                              step 44's mark), BandColor, ParseHex, Wounded, DescribeBar / DescribeMan, HiddenName
  ImpactSpike.cs              step 47, IMPACT I1 pure (AI_NOTES "Step 47", RESEARCH §R10): SpikeLever (the six), SpikeVerdict (Unseen /
                              Works / Fails), StaggerKind, ForceRefusal (Gone, NotHuman, Mounted, GameObject, Water, Down - in the
                              order asked), WatchKind / WatchEvent, ActionRead (both channels' type + index), SpikeHit (one collision),
                              ImpactActions (the engine's codes - the smoke checks them; IsStrike / IsFall / IsRecoil / IsDefend),
                              SpikeRules (live: ModEnabled FIRST, ImpactSpike; Describe), KickPlan, SpikeMath - the loud values as
                              documented constants (StunFactor 3, StunCapSeconds 2, ReactionSpeed 0.5, every 10 / 20 s, 1 damage, the
                              windows, the pools, MaxActionsPerTick 20), Kick (the 4-kick rotation: KnockDown untouched / reel untouched /
                              KnockDown slowed / reel slowed), StunTreated (an EVEN blocker index), StunAsked, Due, IgnorePriority (it
                              alternates), KickLanded, CountVerdict / LengthVerdict / Combine, the names, NextStep (what each verdict means
                              for steps 48-53 - IMPACT §7's fallbacks); ReactionWatch (a struct state machine over the channel reads:
                              Appeared / NewAction - a _continue - / Ended with its length / NeverAppeared / Capped)
  ImpactSpikeStats.cs         step 47: BlockStunTally (lever 2's worker-thread counters - Interlocked only), ImpactSpikeStats (per lever:
                              asked, seen, the lengths treated vs control - MeanStd -, refusals, disabled levers, the cost) + Verdict
                              per lever + the 7 [summary] "impact spike" lines (one when never on)
  SpreadStats.cs              MeanStd (Welford, population std), FormationAthleticsStats (squad
                              mean ± std, band, mean f, at full strength, mean health - step 9; step 23:
                              Bracing / Ready / Total - the next step's "ready men"),
                              RunSpeedCheck (engine top / asked / moving by f); 5c's
                              attack-interval classes went in 5e
  AthleticsStats.cs           per-mission Athletics counters + the [summary] text (pools from the
                              skill, blows by kind, riders, detection cross-checks, kicks / bashes
                              charged apart - by channel, at the hit, free (step 18), blocks paid by
                              the defender - by kind, by you / AI heroes / other AI, free, off, the
                              same blow again, missiles into a shield (step 22), the delay restarted vs
                              kept running by kind (step 29), free actions,
                              exhaustions + peak zone, fighter-time by f, heroes, player,
                              formations, health cap, regen by effort and its curve, refills from
                              empty to the peak line (step 14), run-speed checks by f,
                              recomputes, walk vs run speeds, tick cost, errors)
src/TraxCombat.Module/        net472 — the Bannerlord module, TraxCombatEnhancements.dll:
  SubModule.cs                entry point: load log (+ "module: <Id>" - which copy runs), config
                              init/re-reads, MCM register/retry (the module list handed to McmBridge -
                              step 12), the two model decorators
                              (OnGameStart), AthleticsLogic per mission + its PlayerAttackGate at
                              index 0 (step 13); step 11: claims SingleCopy
                              FIRST in OnSubModuleLoad - refused = _inert (an INSTANCE flag: one
                              assembly may serve both modules), every hook returns at once; the
                              running copy's ReportCopiesOnce ([compat] + one message, main menu)
  ModPaths.cs                 Configs\TraxCombatEnhancements\ via EngineFilePaths.ConfigsPath
  ConfigStore.cs              config.json ↔ TraxSettings.Shared: first run, re-read at game and
                              mission start, write after MCM Done by the rewrite rule, backups;
                              the [config] defaults: line; RevertAllToDefaults, ExportDefaults;
                              step 13: ConfigFile.Migrate at every read (logged "migrated config.json …",
                              the file rewritten as the current format) and on the MCM-save re-read
  TraxLog.cs                  trax_combat.log: tagged lines, trimmed past LogMaxMegabytes (read live) to
                              half by LogTrim (a failed trim retries after 1 MB), Verbose (rate-limited,
                              only when VerboseLogging, written "~[tag]"), VerboseWants(bucket) (R8: the
                              hot paths ask it before building a line), Limited (always, rate-limited per bucket),
                              Error (stack, rate-limited, in-game notice); ONE handle kept open
                              (AutoFlush, shared read/write/delete - read it with FileShare.ReadWrite),
                              Release() at every mission end and at unload (step 10a)
  Mcm/McmBridge.cs            the MCM page — fluent builder, MCM types in METHOD BODIES ONLY,
                              no MCM-typed lambdas (read its class doc before touching it);
                              step 12: first try at the main menu (MCM builds its services there), none
                              with MCM's module off, retries 1/s capped by Core McmPlan;
                              group "Defaults": the Revert / Save-defaults-file BUTTONS
                              (ProxyRef<Action>, page refresh via PropertyChanged), just above
                              Advanced (MCM order = 2 x the schema's; MCM's UI sorts ascending)
  Models/TraxDamageModel.cs   AgentApplyDamageModel DECORATOR — forwards everything; overrides
                              ApplyGeneralDamageModifiers: BaseModel first, then the roll
                              (our exceptions → the game's value); step 47: DecideAgentKnockedDownByBlow = the base's answer OR
                              ImpactHooks' decider, CalculateDefendedBlowStunMultipliers = the base, then ImpactHooks (any thread)
  Models/ImpactHooks.cs       step 47, the impact system's MODEL seams: IKnockDownDecider (main thread, every collision on a man on foot)
                              and IBlockStunDecider (ANY thread - reads only) plugged in by the running logic; KnockDownOnBlow (OR-ed,
                              a throw = the game's answer), AfterDefendedBlowStun (a throw = the base's stun kept, the exception handed
                              to the main thread - TakeStunFailure; while our cap is raised every stun nobody lengthened keeps the game's
                              own cap), RaiseStunCap / RestoreStunCap (ManagedParameters StunPeriodMax, the game's value remembered once,
                              a higher cap left alone), InOwnBlow ([ThreadStatic] - our blow inside RegisterBlow)
  Models/DamageRandomizer.cs  feature 1, game side: game structs → HitFacts → Decide → roll with
                              the attacker's f (the rider's on a horse charge) as the upside,
                              stats, [damage] lines (mission start, first roll + thread,
                              verbose roll/skip), the [summary] damage block; step 25: Reduce -
                              AFTER the roll (or on the game's value when the roll is skipped),
                              DamageReduction's rules, the first reduced hit logged in full, ", reduced
                              → N" on the verbose lines, the reduction in the mission-start line
  Models/TraxAgentStatModel.cs AgentStatCalculateModel DECORATOR — forwards everything;
                              UpdateAgentStats: base first, then the attack ANIMATIONS × the line
                              A + (1 − A) f, f read off the applied m (step 20b; A =
                              AttackAnimationMinPercent, 100 = full speed - step 13), the run
                              multiplier (+ the AI's attack values while AttackRateAiDecisions,
                              5e - off by default since 13), or a slowed rider's horse's
                              (SpeedFactorsFor - managed reads only); step 30: a tired AI man's
                              AiRandomizedDefendDirectionChance = TiredBlockMath.Chance(the base's, w) - never
                              the main agent; step 43: an AI man on foot's two melee attack-decision chances × his aggression
                              share (SpeedPenalty.ScaleAttackDecisions; never the main agent or a rider); + the tournament
                              SetAILevelMultiplier fix
  Models/SpeedPenalty.cs      the penalties on AgentDrivenProperties: attack (swing, thrust/draw,
                              reload), the AI's decisions (5e: attack / riposte / shoot chance x m,
                              the aim ÷ m), run (MaxSpeedMultiplier), horse (MountSpeed) - nothing
                              else (never handling or any defence value) + Snapshot / AiSnapshot
  Missions/AthleticsLogic.cs  MissionLogic in every SP mission (partial - with .Engine, .Api,
                              .Log, .StepBack, .AttackRate, .Hud): lifecycle, start/end
                              lines ("mod ON/OFF"), master-switch toggles ([mission] line each),
                              damage stats reset (AfterStart), the [summary] block
  Missions/AthleticsLogic.Engine.cs  the Athletics engine: per-agent state (by Agent.Index +
                              dense array; a man leaving, a stale record at a reused index and a
                              deleted agent are all dropped by Forget - 10a), the Athletics skill +
                              hero/leader flags at spawn, blow
                              detection (poll ReleaseMelee, OnMeleeHit, OnAgentShootMissile,
                              OnMissileHit), step 18's kicks / bashes (channel 1 + channel 0 on foot
                              - ObserveLowerAction; the hit reads both, then KickOrBashHit;
                              ChargeKickOrBash - no timer, no step back; the YOU line), step 22's
                              blocks (OnMeleeHit's LAST try: BlockHitTaken → BlockMath.KindOf →
                              BlockTaken - the defender's BlockTracker, ChargeBlock, AfterCharge; the
                              YOU line per kind - step 29: "the refill keeps running" or "restarts the refill
                              delay"; OnMissileHit counts missiles into a shield), the health
                              cap (OnAgentHit + every regen step), regen
                              by effort, three speed multipliers re-targeted in 0.05 steps and
                              applied by UpdateAgentProperties (≤ 50 a tick), the horse table
                              (OnAgentMount/Dismount), hot swap, SpeedFactorsFor (the decorator's
                              lookup), the run-speed and walk/run sampling, Failed
  Missions/AthleticsLogic.Api.cs  READ API for steps 6-9: TryGetReading(agent) (points, pool,
                              usable pool, f, peak line, multipliers), TryGetPeakShare(agent),
                              TryGetFormationStats(formation) (incl. mean f and health; the player
                              left out, as the order cards; step 20: EVERY team's formations - slots by
                              Team.TeamIndex, MaxStatTeams 8, one allocation-free pass; step 23: + how many brace),
                              FormationStatsVersion, IsRunning; step 44: TryGetFever(agent, out armed, out share) (an
                              AI man's running fever - never the player, false with the fever off) + its allocation-free
                              walk FeverWalkCount / TryGetFeverAt(i) (0 while no fever runs); step 46: MarkWalkCount(everyone) /
                              TryGetMarkAt(i, everyone, rules, bands, ref FeverMark, out agent) - his fever and his Athletics bar's
                              reading (never you, never a man gone)
  Missions/AthleticsLogic.Log.cs  [athletics]/[speed] lines (verbose buckets) + summary feed
                              (releases every step back before the summary, then its lines)
  Missions/AthleticsLogic.StepBack.cs  step 5d bookkeeping: the roll at every counted swing's
                              END (EndRelease), the queue started from the tick (never in an
                              engine callback), the cap, time read live, every release path
                              (time, order/arrangement/formation change, detach, player, mount,
                              rout, switch-off = all at once, mission end; left the field = no
                              engine call; handed over to a game job = never disabled), the
                              guard count (OnMeleeHit), [stepback] lines, the first one in full;
                              step 16: the body by technique at the START (StepBackBackpedal), the
                              backpedal's wish on / off (on EVERY path; step 17: one the game takes over
                              is still released - our callback off, R27), Steer every tick (the vector,
                              arrived, edge ahead), every-0.25 s samples (the first's written into its
                              end line), a hold running then marked overlapped, the switch logged
  Missions/StepBackBody.cs    IStepBackBody = the ENGINE side of the step back behind one seam
                              (the smoke plays it); GameStepBackBody: mission kind (battle mode,
                              no tournament/arena by behaviour NAME, no naval), vanilla's gate
                              CanBeAssignedForScriptedMovement, orders, target, navmesh checks,
                              SetScriptedPositionAndDirection / DisableScriptedMovement, flag
                              checks; StepBackState / Plan / Snapshot / Release; step 16: Steer (None
                              here), InputStepBackBody (the backpedal: the same probe, hooked instead of a
                              frame, Steer = distance along the checked line + the ground 0.6 m further
                              back every 0.25 s + the away line in his body frame; a game GoToPosition =
                              handed over; release = the input stops, the callback off if idle)
  Missions/AiInputHook.cs     step 16: AiInputState (the logic's wishes - hold, step hold, backpedal +
                              the vector - and the hook's counts / the first man's captured frames),
                              AiInputComponent (AgentComponent.OnAIInputSet: idle = one bool; never the
                              player or a non-AI agent; nothing thrown to the engine), AiInputHook (Hook
                              - the component added lazily FROM THE TICK + the engine's callback on;
                              UnhookIfIdle - off only if ours and no other component overrides the hook
                              (RTS Camera's); SetHold / SetBackpedal / Clear; Apply - the managed filter
                              the smoke drives; FlagNames); step 23: the brace wish (BraceHoldAttacks - melee
                              only: BraceRanged / a ranged action let through; BraceShieldInHand → the shield
                              held up, RaiseShield), SetBrace, the brace's frame counts; Active = ANY wish
  Missions/AthleticsLogic.AiHolds.cs  step 16 shared bookkeeping: EnsureInput, NoteHooked, the
                              component's errors logged from the tick, the GUARD by state (OnMeleeHit),
                              the [summary] "AI holds" lines, HoldsHeader (the technique in the header)
  Missions/AthleticsLogic.AttackRate.cs  steps 5e / 13: every channel-1 action change closes / opens
                              a phase (filed at the band at its start, BEFORE the action's charge)
                              and builds the attack's D (wind-up + release, ranged + the reload),
                              flagging its END; AttackEnded (after the step-back roll) → yours or the
                              AI TIMER (the old pace hold: NoAttack for D x (1/m − 1) after every
                              attack, melee and ranged, riders too - queued, started by TickPace,
                              lifted on every path: time, an attack slipping through, switched off,
                              left the field, the player took him, mission end, a game job waited
                              out); AttackBegan (the gap after the last timer); the ready-progress
                              poll (wind-up vs held); cycles; the guard by f; the switch notes (AI
                              decisions / the animation floor re-applied); [rate] lines (mission
                              start, the first slowed fighter's values, the first AI timer), the summary;
                              step 16: the body by technique at the START (AttackRatePaceByInput - a
                              waiting NoAttack keeps its own), the wish on / off on every path, THE TIMER
                              SURVIVES A STEP BACK (R1's drop gone: by input at once; NoAttack behind a
                              scripted walk DEFERRED - TickDeferred sets it the tick the walk ends or counts
                              it covered), the never-called warning, the first hold's input frames;
                              step 21: AiAttackEnded → NoteClassAttack + BattlePaceMath.Plan → AskPause (the
                              old decision, a reason literal when no pause; a FRESH man is held when his class
                              has a share), the pause's parts in PaceState, the share in the timer rows and the
                              first timer's line
  Missions/AthleticsLogic.BattlePace.cs  step 21: ReadAttackClass at each release (AI only; a failing read =
                              the plain class of its kind, one [error]), NoteClassAttack, SetPaceModel,
                              PaceNotStarted (a refused / superseded pause), NotePaceCycle (by class, the cap
                              + the share inside), NotePaceGap, the settings line at mission start and on a
                              change, FirstOfClass (one [rate] line per class), WriteBattlePaceSummary
  Missions/AthleticsLogic.SwingStack.cs  step 32: the clock (StackOnAction before the phases, StackRegenSample in the
                              regen pass - + the fill by f, StackWaitOver for a waiting AI man, RetargetPlayerWait every gate
                              frame), SpendSwing (releases, kicks / bashes; the YOU lines), StackClamp (after every charge),
                              AskStackPause (the AI: a swing stored = no pause + his own gap as grace; 0 = AskPause with the
                              wait), AskStackWaitAfterKick, PlayerStackHoldIfEmpty, KickGate (your kick bit out at 0),
                              NoteStackSettings, StackPass (the smoke), WriteSwingStackSummary; the player timer, the AI timer
                              (PaceState.StackWait), AiInputHook (HoldKicks / HoldKick) and the recovery read carry the rest
  Missions/AthleticsLogic.Sprint.cs  step 33: TickSprint (from TickAthletics BEFORE the recompute loop: switched off →
                              EndAllSprints; your key every tick - a PRESS starts, letting go ends; PollOrders every 0.25 s -
                              a formation's kind changed = a new order → OrderChangedFor its AI men; ForgetOrders while the AI
                              sprint is off), SprintPass every 0.1 s (Clamp to his points, RunSprint - drain while moving, melee
                              every 0.25 s, the run factor into TrackedAgent.SprintRun in 0.05 steps → SpeedDirty -, Refill),
                              BeginSprint / EndSprint (every path), SprintLeftField, SprintFreshStart (step 19), CloseSprints,
                              WriteSprintSummary, TryGetPlayerSprint (the bar); step 36: OrderChangedFor → TryStartAi (step 37: all
                              above the order's reserve), reserve kept counted + its first line; step 35: SpeedFactorsFor puts SprintRun IN PLACE
                              of the run and hands the decorator SprintTopSpeedReachSeconds (+ remembers the game's reach,
                              TrackedAgent.GameReach); the floor and the acceleration in the first-sprint lines; a sprint setting
                              change marks every sprinter for a recompute;
                              RegenPass skips a sprinter; the recompute budget's start ROTATES (the first man left waiting); step 43:
                              PollOrdersFor - the order poll runs while the AI sprint OR the fever is on and hands each changed
                              formation's men to both (SprintFormationRead.ByPlayer = !Formation.IsAIControlled)
  Missions/SprintBody.cs      step 33: ISprintBody (the smoke plays it) + GameSprintBody (formations by Mission.Teams /
                              FormationsIncludingEmpty, the order by GameBraceBody.KindOf, motion, GetNearbyEnemyAgentCount,
                              the key through Mission.InputManager.IsKeyDown(InputKey) - KeyFor, PlayerCanSprint = a fight
                              mode and the order menu closed), SprintBodyDefault
  Missions/AthleticsLogic.Brace.cs  step 23: StartBrace / NoteBraceSettings (the [brace] lines), TickBrace (switched
                              off → EndAllBraces at once; else a staggered slice so each man is looked at ~every
                              0.25 s), PollBrace (not AI / the player → none; his two rolls once - the margin on BraceDice,
                              the floor on BraceFloorDice (step 28); the order → the band →
                              BeginBrace / EndBrace / KeepBracing), BeginBrace (EnsureInput, Hook, SetBrace, the
                              hands, the shield's first wield, the first brace in full), KeepBracing (hands every
                              look, the shield seen / put away, a wield every 1 s up to 3), EndBrace (every path;
                              the callback released if idle), BraceLeftField, BraceFreshStart (step 19),
                              CloseBraces (mission end), BraceHitTaken (the guard), NoteBraceRelease, WriteBraceSummary
  Missions/AthleticsLogic.TiredBlock.cs  step 30: StartTiredBlock / NoteTiredBlockSettings (the [guard] lines),
                              TiredBlockWeight (RetargetSpeed carries w with the speed multipliers - same 0.05 steps
                              and recompute budget; RetargetSpeed is an instance method since), NoteTiredBlock /
                              TiredBlockApplied (the decorator's write counted by f band, the first in full),
                              TiredGuardHit → GuardTaken (OnMeleeHit, before the block's charge: the AI's guard by f
                              band; you never; step 34: ShieldInHand = step 21's IWeaponFacts read at the blow and
                              at the recompute, a failing read = one [error], counted without), WriteTiredBlockSummary
  Missions/WallBody.cs        step 41: WallState (held, his list slot, vanilla's timer + its last StartTime, the fallback's
                              next rewrite, the next sample), IWallBody (the smoke plays it), GameWallBody (the facts by managed
                              reads + IsRetreating; the slot GetCurrentGlobalPositionOfUnit(agent, true), CurrentDirection,
                              CachedAveragePosition, CachedFormationIntegrityData; Hold = SetFormationIntegrityData(…, true);
                              Release = vanilla's own formula; the timer field _cachedAndFormationValuesUpdateTimer looked up once,
                              read once per man; EnemyWithin = GetNearbyEnemyAgentCount; step 42: TryEnemy = GetTargetAgent, else
                              ImmediateEnemy, an active enemy only; WallState.Leash / NextLeashLook / EverStepped), WallBodyDefault
  Missions/AthleticsLogic.Wall.cs  step 41: StartWall (the [wall] line), TickWall from OnPreMissionTick (the switch / master
                              switch → ReleaseAllWalls at once; every frame each held man whose timer moved is rewritten -
                              LookWall AfterVanilla - or the fallback every 0.1 s; a staggered look every 0.25 s - the gates,
                              hold / let go with vanilla's value, a sample every second look), the held list (swap-remove),
                              WallLeftField (Forget - no engine call), WallFailed, CloseWalls, WriteWallSummary; step 42: the
                              leash in LookWall (only while L > 0 or a man is out - L 0 = step 41's calls exactly; a man out =
                              vanilla's value given back, kept in the list, nothing written - goto Sample), the per-frame loop
                              looks at a man out every 0.1 s instead of after vanilla, Manage (into the list), NoteLeash, EndStep
                              (every let-go path ends a step), the first step out / its end in full, a live L change line
  Missions/AthleticsLogic.Fever.cs  step 43: StartFever / NoteFeverSettings (the [fever] lines), RetargetDecision (the share
                              in 0.05 steps → SpeedDirty; called from RetargetSpeed and every look), FeverOrderFor (from the order poll:
                              arm / re-arm / end), TickFever (switched off → EndAllFevers; a staggered slice, each man ~every 0.25 s),
                              PollFever (player / rider → ended; contact = EnemyWithin(FeverContactMetres) → the burn; the shield by
                              IWeaponFacts; the sprint check; the proof's contact time by group and bar band), FeverSprintCheck (the
                              gates, the two proximity reads, FeverDice → TryStartAi + BeginSprint), EndFever (every path), FeverLeftField,
                              CloseFevers, NoteFeverAttack (StartRelease), NoteDecisionValues (the decorator), WriteFeverSummary
  Missions/ImpactBody.cs      step 47: IImpactBody - the impact levers' ENGINE side, one method per lever (the smoke plays it): IsActive,
                              IsMounted, CanForce (never a horse, a rider, a siege engine / ladder, water, already down), Read (both
                              channels' GetCurrentActionType + GetCurrentAction(ch).Index), SlowReaction (SetCurrentActionSpeed),
                              ForceStagger (SetActionChannel(0, act_stagger_*) - CanForce checked again first), TryShieldInHand /
                              SlotHoldsShield / HasAnyShield (the managed equipment), DropShield (Mission.AddTickAction(DropItem) - queued,
                              vanilla's way), OwnKnockdownBlow (vanilla's drowning blow through RegisterBlow: KnockDown | KnockBack, ≥ 1
                              damage, owned by the attacker); GameImpactBody, ImpactBodyDefault
  Missions/AthleticsLogic.Impact.cs  step 47, THE SPIKE (docs/IMPACT.md §7 I1): StartImpact (the deciders plugged into ImpactHooks, the cap
                              raised while on, the [impact] start line), TickImpact (from OnMissionTick, own try: the switch - master switch
                              first, off = the cap back and everything let go -, lever 2's worker-thread failure, the queued calls ≤ 20 a
                              tick, the watches, the shields), the deciders (KnockDownForKick - the rotation's next kick; SetBlockStun - x3
                              for an even blocker, any thread), ImpactHit (OnMeleeHit's part: a landed kick → the KnockDown / reel watch +
                              the shield drop; a block → the stagger every 10 s, else our blow every 20 s, else lever 2's recoil + guard
                              watches - ≤ 32 slots), the fixed pools of structs (SpikeWatch 64, SpikePending 64, SpikeShield 32),
                              DisableLever (one [error], "lever N disabled", the others run on), ImpactLeftField (Forget), the first-time
                              lines, WriteImpactSummary (CloseImpact first); the logic IS the IKnockDownDecider / IBlockStunDecider
  Missions/BraceBody.cs       step 23: BraceState (his roll, step 28's floor roll FloorUnit, the running brace, the shield's
                              progress), IBraceBody (the
                              engine side - the smoke plays it), GameBraceBody (the order from
                              GetReadonlyMovementOrderReference().OrderEnum + FormationAI.ActiveBehavior for an AI Move;
                              the hands from the wielded indices + managed equipment; TryToWieldWeaponInSlot WithAnimation;
                              AiInputHook.Hook / UnhookIfIdle), BraceBodyDefault (the smoke swaps it once)
  Missions/WeaponFacts.cs     step 21: IWeaponFacts (what he holds at a release - the smoke plays it) +
                              GameWeaponFacts (WieldedOffhandWeapon's usage IsShield; WieldedWeapon's
                              WeaponClass Bow / Crossbow - the slot index is a pointer read, the item managed:
                              safe in a hit callback) + WeaponFactsDefault (the smoke swaps it once)
  Missions/AthleticsLogic.PlayerTimer.cs  step 13, YOUR timer: the hold from the release's start
                              (PlayerAttackStarting), the countdown (PlayerAttackEnded), the gate's
                              frame (GatePlayerInput: native flags → GateFrame, managed - clears
                              AttackMask while held), swallowed presses, the flash, hold-to-attack,
                              missed attacks, releases (switched off, not you, mission end), the tick's
                              safety net, TryGetPlayerRecovery (the recovery bar's read), [athletics]
                              YOU lines; SmokePlayer (the smoke's stand-in for Mission.MainAgent); step 17:
                              only YOUR hands - YouDrive (no pause while the AI drives your hero - RTS Camera's
                              free camera - R25; SmokePlayerAiControlled), a game object in use (a siege engine
                              fires on your attack bits) never held (R26)
  Missions/AthleticsLogic.Hideout.cs  step 19: the hideout boss fight = a fresh start - NoteHideoutMission (first
                              tick: the controller by NAME, MissionObjectiveLogic), TickHideout (top of the Athletics
                              tick: the intro = CutScene mode, the objective by reference), ObserveObjective (the boss
                              objective's id → BossFightBegan once), BossFightBegan (side reads FIRST - Team.IsPlayerAlly
                              / Team.Invalid = aside; a throw = one [error], nothing refilled - then FreshStart + every
                              release: Finish / EndHold / ReleasePlayerTimer (FreshStart), a queued / deferred one
                              dropped, ResetPhases, RetargetSpeed exact); HideoutSideOf = the smoke's teams
  Missions/PlayerAttackGate.cs  step 13: a MissionLogic added then moved to INDEX 0 of
                              Mission.MissionBehaviors (SubModule, OnMissionBehaviorInitialize) - it
                              pre-ticks right after MissionMainAgentController wrote the input
  Missions/PaceBody.cs        PaceState (+ D, kind, the attack's end, the pause); IPaceBody = the AI
                              timer's ENGINE side (the smoke plays it); GamePaceBody: NoAttack via
                              SetScriptedFlags only on a free man (no GoToPosition / NoAttack / object
                              / ladder / detachment; riders allowed since 13), lifted only while he is
                              free, else Waiting; step 16: InputPaceBody (no flag - hooked, the same
                              refusals, never waits; release = the callback off if idle); PaceState +
                              ByInput, Overlapped, Deferred, the call count at the start, hits taken;
                              step 21: the pause's parts (Tired, Share), its Class and Percent
  Missions/TrackedAgent.cs    one fighter's record (step 22: + Block, his BlockTracker): Core Fighter + detection, f-bin, fresh top
                              speed and slowed-horse fields, his StepBackState (null until needed),
                              the attack-rate phase state (step 20b: + the phase's animation multiplier),
                              the running attack's D (at full animation speed) + as played and its end, his
                              last rest by kind, his last timer (for the gap), his PaceState, his
                              AiInputState (step 16, null until first held by input); step 21: the attack's
                              class (read at its release), the last ended AI attack's class / share / model for
                              the next cycle note, the classes he attacked with, his last timer's class
  Missions/AthleticsLogic.Hud.cs  step 6: AttachHud on the logic's FIRST TICK (the screen runs by
                              then - RESEARCH §G) - each view via MissionScreen.AddMissionView with
                              a GauntletHudLayer, "[hud] attached:" lines; WriteHudSummary (the
                              [summary] hud: lines + each view's own; the screen finalizes views
                              before it). Attached: the player bar, the recovery bar, the orders strip
                              (step 9, with its GauntletOrderCards + MissionStripFormations), the ALT
                              markers (step 20, with GauntletFormationMarkers + ProjectedFormationMarkers), the fever
                              marks (step 44, with GameFeverMarks over a second GauntletFormationMarkers - since step 46 the
                              "ALT bars": every man's Athletics bar above his fever bar)
  Hud/TraxHudView.cs          THE BASE OF EVERY HUD VIEW (MissionView): one GauntletLayer + movie +
                              VM that exists exactly while HudGate says so, read every frame
                              (ReadFrame → Tick(in HudFrame) - the smoke drives Tick); refresh every
                              HudRefreshSeconds; OnLayerFrame every frame (step 9); suspend/resume;
                              Finish at mission end; every entry wrapped - an error or a movie that
                              does not load disables the view for the mission (layer removed,
                              [error], "[hud] … DISABLED"); [hud] lines for build / removal / not
                              shown, each with its reason, in the view's OWN rate buckets (step 13:
                              hud-layer:<view>, hud-outside:<view>) (QuietConditionToggles: a view's own
                              condition coming and going → verbose after the first build); HudStats;
                              step 12: OutsideToggle / ReadOutside = a view's outside-a-battle rule (the
                              grace clock; builds / removals outside a fight in the "hud-outside" bucket);
                              step 20: ReadsIndicatorKey (the frame reads the game's ALT key for that view); step 46: ToggleOn (the
                              view's switch read live - a view may take two) + ToggleName (the log's words for it)
  Hud/HudLayer.cs             HudFrame (one frame as a view sees it; IsFightMode = Battle, Duel,
                              Tournament, Stealth; step 12: IsWalkMode = StartUp, PlayerWeaponDrawn by
                              HandsFull; OrderMenuOpen; step 20: ShowIndicatorsKey - game key 5, Left Alt),
                              IHudLayer (the engine seam), GauntletHudLayer
                              (vanilla's recipe: IsCustomType check, LoadMovie, AddLayer; release
                              the movie BEFORE RemoveLayer; a failed movie never goes on screen)
  Hud/PlayerAthleticsView.cs  step 6, DESIGN §3.1: the player's bar - TryGetReading(main) → BarMath
                              → the VM; logs the first values per layer and the FIRST time of each
                              colour / empty / wound per battle (later colour changes verbose only);
                              step 12: outside a battle too (ShowPlayerBarOutsideBattles, BelowFull)
  Hud/PlayerAthleticsVM.cs    its ViewModel - every property EXACTLY the bound widget property's
                              type (float / Color / bool / string: Gauntlet converts only strings);
                              change-checked setters; the number text rebuilt only when it changes
  Hud/AttackRecoveryView.cs   step 13 (Anton's "attack recovery" bar): just above the Athletics bar -
                              EMPTY at your attack, FILLING over your pause with the seconds inside,
                              FULL otherwise; the flash on an early press; read every frame
                              (OnLayerFrame), layout at the refresh; the Athletics bar's gate + its own
                              switch + ShowPlayerBar and AttackRatePlayerTimer on; first values / first
                              pause / first flash lines (bucket hud-recovery), the summary line
  Hud/AttackRecoveryVM.cs     its ViewModel (layout, Fill, FillColor, SecondsText / SecondsShown, FlashOn)
  Hud/SprintBarView.cs        step 33: the sprint bar above the recovery bar ("Sprint 6.2 s", the store's fill, the start mark,
                              grey / blue / yellow) - read every frame, layout at the refresh; fights only, ShowSprintBar +
                              ShowPlayerBar + SprintEnabled; first values / first sprint lines (bucket hud-sprint), the summary
  Hud/SprintBarVM.cs          its ViewModel (layout, Fill, FillColor, MarkOffset / MarkShown, LabelText by the tenth)
  Hud/OrderCards.cs           step 9's engine seams: IOrderCardSource / GauntletOrderCards (the
                              "MissionOrder" layer found with the public FindLayer, its widget tree
                              walked once per open - card = OrderTroopItemBrushWidget in a two-widget
                              slot - and each card's pixels / visibility / CurrentMemberCount read
                              every frame; nothing patched), IStripFormations / MissionStripFormations
  Hud/OrderStripView.cs       step 9, DESIGN §3.4: the orders-menu strip - while the menu is open,
                              a cell under each card (placed every frame in pixels, slot k checked
                              against formation k), the fallback panel (reasons, 0.5 s / 1 s grace),
                              values every refresh, [hud] first placement / card changes / fallbacks
                              / values lines, the [summary] strip line; step 24: the ready count per cell
                              at each values push (ReadyMath; ReadyStats + its [summary] line)
  Hud/OrderStripVM.cs         its ViewModels: the root + 8 fixed OrderStripCellVM in one
                              MBBindingList, bound TWICE by the prefab (the cells, the panel rows);
                              step 24: ReadyText / ReadyColor / ReadyShown / ReadyMarginTop, SetReady,
                              ReadyColors (shared with the ALT labels)
  Hud/FormationMarkers.cs     step 20's engine seams: IFormationMarkerSource (TryReadShown = vanilla's own
                              "markers shown", Read → MarkerRead Live / PointsOnly / Projected / Unavailable),
                              GauntletFormationMarkers (the "MissionFormationMarker" layer by the public
                              FindLayer, GetMovieIdentifier("FormationMarker").DataSource = the game's
                              MissionFormationMarkerVM - IsEnabled, Targets' Formation / ScreenPosition / WSign /
                              Distance / TeamType / Size - and its FormationMarkerListPanel widgets' Position,
                              Size, AlphaFactor, IsTargetingAFormation, own offsets; nothing patched),
                              ProjectedFormationMarkers (the fallback: every team's formations, the median's
                              ground + 3 m through WorldToScreen from the combat camera, nominal sizes)
  Hud/AltMarkerView.cs        step 20, DESIGN §3 item 3 (hold ALT): shown exactly while vanilla shows its
                              markers (its flag; else ALT or the orders menu copied; NeedsPlayer false); every
                              frame: read, pair (or nominal), Sight, Place each label under its marker, values
                              when the stats / settings version moves; fallbacks (a marker without its widget,
                              no widgets, no layer → own projection for the show) once per show, the first of
                              each in full; [hud] first-show line (technique, markers, labels, no-label reasons),
                              values line, the [summary] line (AltMarkerStats); step 24: the ready count in
                              PushValues (every side; ReadyStats + its [summary] line)
  Hud/AltMarkersVM.cs         its ViewModels: the root + a GROW-ONLY MBBindingList of AltMarkerLabelVM keyed by
                              team + formation (a label never swaps formations when vanilla re-sorts); step 24:
                              ReadyText / ReadyColor / ReadyShown, SetReady
  Hud/FeverMarkSource.cs      step 44's engine seam: IFeverMarkSource (TryReadShown = vanilla's "markers shown" flag, NewShow,
                              BeginFrame - the camera, the screen in pixels and the UI scale -, Locate - side, the feet +
                              FeverMarkHeightMetres, distance, in front -, Project, Describe; the smoke plays it); GameFeverMarks
                              (step 20's GauntletFormationMarkers for the flag, CombatCamera Position / Direction, Agent.Team /
                              IsActive / Position, MBWindowManager.WorldToScreen; the scale from our own layer's UIContext);
                              step 46: Locate sets Mounted (Agent.MountAgent, managed) and lifts a rider RiderLiftMetres
  Hud/FeverMarkView.cs        step 44, DESIGN §3 item 3b (hold ALT): shown exactly while vanilla shows its markers (FeverEnabled
                              FIRST in the condition, then its flag, else ALT / the orders menu copied; NeedsPlayer false); every
                              frame: the logic's fevers walked (TryGetFeverAt), Locate, PreSkip, Project + on screen, Pick the
                              nearest FeverMarkMax, Size / Place / Tint × vanilla's fade into the pool; [hud] first-marks line,
                              "FeverEnabled switched off", the [summary] line (FeverMarkStats, its own stopwatch); vanilla's own
                              per-man icon (MissionAgentLabelView's mesh) never touched. STEP 46 (§3 item 3c): named "ALT bars" in
                              the log; the toggle = ShowAthleticsMarks OR ShowFeverMarks, the condition = something to draw
                              (FeverEnabled or the Athletics bars) then the flag; the walk = every man (TryGetMarkAt) with the bars
                              on, AthleticsMarkMath.Decide, PickOnScreen through a struct Projector, Layout (the stack), the band /
                              wound / fever colours × the fade; the first-bars line (men looked at, in a fever, marked, bars by
                              colour, the first drawn - "calm" / "armed"), "… (the Athletics bars stay)"
  Hud/FeverMarksVM.cs         its ViewModels: the root + a GROW-ONLY POOL (MBBindingList of FeverMarkVM - mark i = the i-th
                              drawn this frame, the rest hidden); FeverMarkVM: IsShown, PositionX / Y, Width / Height (Scaled*
                              pixels), Fill, FillColor, FrameColor (the fade's alpha baked in); step 46: the box (PositionX / Y =
                              its top-left, BoxWidth / BoxHeight), FeverShown, the Athletics bar - BarShown, BarWidth, BarHeight,
                              BarFill, BarUsable, BarColor, BarFrameColor, BarWoundColor
tests/TraxCombat.Core.Tests/  net8.0 xUnit (697) — schema vs DESIGN.md (keys + types), one copy
                              runs (SingleCopyTests: claims on private slots, copies, texts), when
                              MCM is tried and when it stops (McmPlanTests - step 12),
                              defaults.json (DefaultsFileTests), master switch, settings, config
                              file, merge rule, rate limiter (+ Peek), the log trim (LogTrimTests:
                              kept kinds, one cut point, stacks, notes, the last resort, bytes),
                              damage roll/rules/dice/stats/upside, the damage reduction
                              (DamageReductionTests, step 25: 100 → 75, 0, the 90 clamp, rounding,
                              never below 1, 0 stays 0, which hits, the roll off still reduced, the
                              master switch, the average after the roll, the summary line),
                              Athletics v2 rules (pool, f, cap, curves, effort regen, the refill curve
                              - T for any k, 100 = the old rule to the bit, exact steps - DESIGN's
                              blow counts), kicks and bashes (KickBashTests, step 18: 3 / 2.25 / 1.69,
                              free at 0 and off, one decision per action across both channels and the
                              hit), blocks (BlockTests, step 22: 1 / 5 / 2 x the multipliers, live, drain
                              and the refill delay, free at 0 and off, the classifier, once per blow, the
                              refill at 100 exactly linear, the format-5 migration), only a wrong-side block stops the
                              refill (BlockRefillTests, step 29: the rule on / off, the clock and the running
                              refill untouched, live, the master switch, a hero parrying every 1.5 s from 0
                              climbs +1 a parry, a floor soldier parrying sinks, the summary split), the hideout boss fight (HideoutBossFightTests, step 19: the refill to the
                              wound cap, the ids and text ids, the gate, the line and the summary texts), mean/std,
                              the run-speed check, Athletics summary, the
                              attack rate (rules, AI values x / ÷ m, the cap, verdicts, phases /
                              cycles / timers / holds / guard + summary; step 20b: the slower swing in the
                              target and the floor), the timer (AttackTimerTests: the pause, the animation
                              LINE by f (step 20b: 1 / 0.85 / 0.925, 100, 0, f off m, D at full speed), the
                              countdown text, the flash, YOUR timer as the gate drives it, the recovery
                              read), the config migration (step 20b: format 3 → 4 through the seam),
                              step back (chance by f, dice, spot, facing, stats, summary), step 16's
                              AI holds by input (AiInputTests: the bit rule, the move bits, the
                              backwards vector in any frame, distance covered, the later end, the
                              switches, the GUARD / hook / overlap lines, the step-back technique and
                              samples, stepped-back cycles and the timer's floor), the bar
                              (bands by f on DESIGN's fighters, shares, numbers, colours), the HUD
                              gate (order, master switch first; step 12: outside a battle - the
                              walk-about mode, weapon / refilling / grace, BelowFull) and HUD stats +
                              summary, the
                              orders strip (matching card sets, cell placement at any scale and
                              the lift, texts, band, health in the squad stats, summary), the ALT markers
                              (AltMarkerTests, step 20: pairing through a re-sort / a missing widget, sight,
                              placement at any scale and pinned, vanilla's fade, nominal size, summary), the
                              battle pace (BattlePaceTests, step 21: the class from what he holds, the pause
                              for each class fresh and tired - the percent fewer at every m -, archers' extra,
                              0 = off, ranges, the settings sentence, the summary lines), the brace (BraceTests,
                              step 23: the floor by order, the hysteresis, the wound cap's band, an order change,
                              the per-man margin, the switches, the shield step and RaiseShield, the summary, the
                              formation's Ready / Bracing; step 28: the no-formation floor, each man's own floor roll -
                              range, apart from the margin, live, clamped at 0 / 100, the hysteresis, the wound cap), the swing stack (SwingStackTests, step 32: the cap - half away
                              from zero, the floor at empty, the examples -, spending, the refill per pause at the current m,
                              the clock stopped while attacking, the AI's gap, the clamp, bursts, fresh starts, live rules, the
                              switch and the master switch, your countdown's retarget, the bar's count, the summary), the sprint (SprintTests,
                              step 33: the store = points / 20, the clamp, refill 7 / 14 / 14 / 28 s, the 20% gate, the profile
                              1.6 to 2.25 s then 1.35 / 1.1, a 30% store 2.25 s long, 0.05 steps, drain only moving, the rest kept,
                              AI shares and order groups, the switches, the key names in the file / defaults / MCM, the summary;
                              step 35: the fresh-run peak 2.0 → 1.1 / exhausted 2.0 → 0.33, the 2 s floor (points 0, pool 50 at
                              50%, pool 150 full), its slow refill, the acceleration cap on / off / 0, live, the master switch;
                              step 37: each order's reserve on Anton's examples (advance 100 / 90 / 60 / 45%, move 100 / 90 /
                              60%, charge all), keep above the store, a move → a second move = reserve kept → the charge takes
                              the 80%, live, the master switch, the summary, the format-7 migration (use keys dropped, old
                              reserve defaults moved, user values kept) and step 33's shares dropped),
                              the ready count (ReadyCountTests, step 24: hidden
                              rules in order, bands at or below 75 / 50, text, colours, the strip cell one row
                              deeper only while drawn and the 1080p bottom card lifted, log text, summary), the
                              tired block direction (TiredBlockTests, step 30: w at f 1 / 0.5 / 0 x 0 / 50 / 100, never
                              the player, the master switch first, the chance own + (1 − own) x w with a negative /
                              NaN / > 1 game value, w 0 untouched, the setting live and placed, the sentence, the
                              summary lines; step 34: the shield / no-shield split adds up, its 2 lines), the shield
                              wall's hold (ShieldWallTests, step 41: the gates in order with the master switch first, the three
                              arrangements, every order, the escape - far AND behind, far ahead held, no speed never far -, ahead
                              of the slot on any facing and a non-unit one, the switch live and on by default, the sentence, the
                              samples' part ahead, the summary lines), the leash (ShieldWallLeashTests, step 42: 0 = the hard
                              hold, freed below L with an enemy close, held at L, back in the slot before the next step - min(0.3,
                              L/2) -, no enemy = held, a man first seen in his slot, a live L change, the reach from the slot and
                              its hysteresis, a square's outward side, the setting placed and live, the switches, the sentence,
                              the summary line), aggression and the fever (FeverTests, step 43: the share by
                              the bar 60 → 20, 0.05 steps and no floor, the player / switches, x 5e's m, the store from current
                              points, armed = the absolute peak, contact, the shield live mid-fever, the fade to the CURRENT tired share,
                              re-arming, an order's end, the sprint chance x the fever left, the roll cadence, the reserve cap, the
                              settings, the format-8 migration 10 → 3 / 5 → 2 with tuned values kept, the summary lines), the
                              fever mark (FeverMarkTests, step 44: orange armed / at the burn's start, through yellow, alpha 1 →
                              0.45, gone empty; vanilla's 1.5 → 8 m fade; 60% size at 60 m and the UI scale; the placement and on
                              screen; the skips in order; the nearest-N pick and the reused / grown frame; hidden reasons with the
                              master switch first; the settings live and grouped; the sentence; the summary line), the Athletics bar
                              above every man (AthleticsMarkTests, step 46: the fill / band per reading at the peak line 75, the wound
                              cap and the fill never past it, the band colours = BarMath's hex, the stack - bar + gap + fever bar, the
                              fever place kept / dropped, only the fever bar = step 44's mark, UI scale and 60% far away, a rider 1 m up;
                              who gets which bar by side with each switch, behind / too close, hidden reasons with the master switch
                              first, the settings live / grouped / ordered; PickOnScreen - an off-screen man nearer takes no slot, only
                              what is needed projected; the summary with colours; the log's "calm"), the format-9 migration (FeverTests:
                              a format-8 file's CostPerBlow 3 → 7 once, a tuned one kept and noted, formats 5 / 7 from 10 end at 7, an
                              older file's 3 kept), the impact spike (ImpactSpikeTests, step 47: the master switch first, the setting
                              Advanced / off / live / before VerboseLogging, the sentence; the 4-kick rotation and a disabled lever's
                              part off, the even-blocker split, x3 held at the cap, Due / IgnorePriority, a landed kick only; the watch -
                              a reel on either channel through a second strike, a fall through its _continue, never shown, a recoil, a
                              guard from the block, the stagger we started, the cap; the count / length verdicts and a FAIL winning; every
                              lever's next step for 48-53; the stun tally from 4000 parallel calls; each lever's verdict, a disabled lever
                              = FAILS, the 7 summary lines, the never-on line). They
                              run on DESIGN's INITIAL values (DesignTable.cs: a module
                              initializer), so tuning defaults.json never breaks them. Keep green.
module/SubModule.xml          release manifest (Id TraxCombatEnhancements, v1.0.0) - THE one home of the
                              version (the DLLs read it at build; stamped once, on release day)
module/GUI/Prefabs/           the HUD movies - file name = movie name, `Trax…` (prefab names are
                              global across modules): TraxPlayerAthleticsBar.xml (step 6: native
                              sprite BlankWhiteSquare_9 + brush AgentHUD.Interaction.Text only;
                              FillBarWidgets draw shares; no widget takes mouse events),
                              TraxAttackRecoveryBar.xml (step 13: the label - step 32: @LabelText, "Swings 4/8" -, a frame, the fill, the
                              seconds centred on it, the flash overlay - same native sprite / brush), TraxSprintBar.xml (step 33: the
                              label @LabelText, a frame, the fill @Fill / @FillColor, the start mark @MarkOffset - same sprite / brush),
                              TraxOrderStrip.xml (step 9: {Cells} ItemTemplates - the cells placed
                              by Scaled* pixel bindings, the panel rows; the ± band = a FillBarWidget
                              ChangeWidget; a list widget carries no @binding - its own bindings
                              would resolve against the list), TraxAltMarkers.xml (step 20: {Labels}
                              ItemTemplate - a centring box per formation placed by Scaled* pixel bindings,
                              the numbers in vanilla's own marker brush NameMarker.Distance.Text, the
                              strip's bar; step 24: + "ready 34/50" under the bar, centred), both with
                              step 24's ready TextWidget (Brush.FontColor bound; the strip's under its
                              bar at @ReadyMarginTop, the panel's after the health), TraxFeverMarks.xml (step 44:
                              {Marks} ItemTemplate - one small bar per mark placed and sized by Scaled* pixel
                              bindings, a dark frame @FrameColor, a FillBarWidget inside 1-px margins @Fill /
                              @FillColor; BlankWhiteSquare_9 only; step 46: each item a box @BoxWidth x @BoxHeight holding the
                              Athletics bar at its top - frame @BarFrameColor, inside @BarWoundColor, the reachable part @BarUsable,
                              the fill @BarFill / @BarColor, @BarShown - and the fever bar at its bottom, @FeverShown)
tools/deploy.ps1              build → AssemblyGuard → OfflineSmoke → install as
                              Modules\TraxCombatEnhancements.Dev "Trax Combat Enhancements (dev)",
                              module\GUI copied beside bin
tools/package.ps1             step 11, THE RELEASE: manifest gate (release Id + Name, vX.Y.Z) → build →
                              unit tests → AssemblyGuard on both DLLs (hard) → OfflineSmoke → DLL version
                              = manifest → dist\TraxCombatEnhancements from scratch (SubModule.xml, our 2
                              DLLs + pdbs, GUI\Prefabs - checked against that list: never MCM, Newtonsoft
                              or game DLLs) → dist\TraxCombatEnhancements_vX.Y.Z.zip (forward-slash
                              entries; an existing one only with -Force) → file list + sizes. dist\ is
                              git-ignored
tools/WORKSHOP-UPLOAD.md      the release loop, step by step (first upload, updates) + the uploader's quirks +
                              "The images" (where the F12 shots live, the gallery list in order)
tools/WorkshopCreate.xml      the FIRST upload (creates the item, Private, tags, preview .jpg, the short pitch) -
                              run once; paths = this PC (C:\Users\Asus ROG\Documents\GitHub\..., C: Steam game)
tools/WorkshopUpdate.xml      every later upload - ITEM_ID placeholder until the first upload
tools/make_workshop_update.py step 26, from the sibling: WorkshopUpdate.xml → a one-off task in %TEMP% that also
                              sends STEAM-DESCRIPTION.bbcode (escaped, &#10;) and --visibility; --notes required
tools/STEAM-DESCRIPTION.bbcode  the Workshop page (≤ 8000 bytes; 3042 now - step 31's short rewrite): the hook, 5 idea
                              bullets (rest your troops, fresh shock troops into tired lines), "How it works, at a
                              glance", the link to docs/HOW-IT-WORKS.md, "Good to know" (NOT an overhaul, RBM named
                              politely as the deep one)
tools/preview_thumbnail.html  the Workshop preview TEMPLATE (PANEL_* placeholders) → make_thumbnail.py
tools/preview_thumbnail.jpg   THE preview (1024², ~275 KB, under Steam's 1 MB): a real battle, the hold-ALT
                              close-up ("ready 1/9" vs "ready 37/53"), your two bars, name + "Slower, tactical
                              battles. Heroes matter."
tools/make_thumbnail.py       step 26 (the sibling's recipe): crops (SHOTS) + paint-outs (ERASE) from
                              screenshots/originals → fills the template → headless Edge → the .jpg
tools/make_gallery.py         step 26: the Workshop gallery, docs/screenshots/01-05 (2560 px, < 1 MB each),
                              in order with captions
docs/screenshots/             the gallery (committed; README shows 01). The RAW F12 shots go in /screenshots/
                              (git-ignored) - Steam keeps them in <Steam>\userdata\<id>\760\remote\261550\screenshots
tools/AssemblyGuard/          soft-dependency guard (from the sibling): MCM, Harmony, ButterLib,
                              UIExtenderEx, NavalDLC, CustomBattle in any type surface = FAIL
tools/OfflineSmoke/           the real DLL on .NET Framework with the game's DLLs, no game
                              launched: types load without MCM, config flows, tournament fix,
                              damage (Program.Damage.cs: the real decorator over the game's
                              CustomAgentApplyDamageModel, fed the game's own hit structs),
                              Athletics (Program.Athletics.cs: the real stat decorator, damage
                              decorator and AthleticsLogic on uninitialized Agent objects - the
                              curves blow by blow, DESIGN's blow counts, the health cap, the
                              upside by f, horses; step 18: kicks and bashes - the costs, once each
                              on either channel or at the hit, no timer / roll, 0 / off, the YOU line,
                              the game's action codes; step 22: blocks - Core's classifier on the game's
                              own AttackCollisionData and result codes, the prices by kind and by who, once
                              per blocked blow, no pause / roll, a recruit emptied by blocks, live, 0 / off;
                              step 29: BlocksLetTheRefillRun - a parrying hero at 0 climbs through the real
                              BlockTaken, wrong-side blocks hold a man at 0, the switch live; the older steps
                              pin OnlyWrongSideBlocksStopRefill off in AthleticsDefaults),
                              the step back (Program.StepBack.cs: the real
                              logic's bookkeeping with a stand-in IStepBackBody - rolls by f,
                              queue, cap, live time, every release path, logs, summary), the
                              attack rate (Program.AttackRate.cs, step 13: full-speed animations,
                              the AI timer D x (1/m − 1) - melee, ranged from the reload, a throw,
                              riders - the gap = the timer, every lift path with a stand-in IPaceBody;
                              YOUR timer through the real logic via SmokePlayer + GateFrame: the hold
                              from the release, swallowed presses, the flash, hold-to-attack, a
                              missed attack, the switches, ranged; the gate FIRST in a stand-in
                              mission's behaviour list; the first-slowed line through the real
                              decorator; R1 re-checked for step 16 - refused = held, started = the
                              NoAttack hold deferred then set, a short pause covered = counted), the AI
                              holds by input (Program.AiInput.cs, step 16: the Core bits vs the game's
                              enum, the override check, the real logic with stand-in input bodies - the
                              timer by input, AiInputHook.Apply frame by frame, the timer surviving a
                              backpedal both ways round, arrived / edge ahead, the master switch,
                              leaving the field, the never-called warning, the switch back to the old
                              ways, OnMeleeHit's guard by state, the summary; the older steps run the
                              OLD techniques - AthleticsDefaults pins them), the decorator's animation floor (100 / 0 / 60), the master
                              switch (step backs released, AI timers lifted, your countdown released,
                              the bar, the strip and the ALT labels removed too; step 19: a hideout boss fight refills nobody), the
                              hideout boss fight (Program.Hideout.cs, step 19: the game's MissionObjective type with the
                              boss id and names, stand-in teams - the battle refills the whole side to a wound's cap and
                              ends every step back / pause / queued one / your pause, the duel you alone, off = nobody,
                              a failing read = one [error] and nothing refilled, the lines), the recovery bar (Program.Recovery.cs:
                              its prefab, the real view over a running timer), the HUD (Program.Hud.cs: PrefabIsValid - any prefab against the
                              game's own widget types, properties, brushes, sprites and the VMs'
                              property types, DataSource lists into their ItemTemplates, no @binding
                              on a list widget; the real PlayerAthleticsView driven by made-up
                              HudFrames with a FakeHudLayer - every hide reason, colours, wound,
                              empty, refresh, summary; step 12: outside a battle - the walk-about mode,
                              a weapon drawn, the grace, refilling, menus, the switch; the fail safe),
                              the orders strip
                              (Program.Strip.cs: the real OrderStripView with stand-in cards and
                              formations - layouts, UI scale, RTS Camera's set, the lift, values,
                              live switches, every fallback, quiet reopen, summary, fail safe),
                              the ALT markers (Program.AltMarkers.cs, step 20: the prefab; the real
                              AltMarkerView with stand-in marker sources - vanilla's flag and the copied rule,
                              labels under the markers at UI scale 1 and 4/3, a re-sort, colours / values,
                              the enemy switch, behind / faded / not laid out / pinned, live settings, every
                              fallback incl. our own projection, the gates, the master switch, summary, fail safe),
                              defaults read from the embedded defaults.json, the MCM page built
                              by MCM's real builder (its group order: Master switch first, the
                              Defaults buttons above Advanced, Advanced last) and its two buttons
                              clicked; step 12: MCM's DLL loaded with its module off (one line, no
                              try) and MCM never ready (31 attempts, one give-up line); step 10a: the log writer (readable while held, new path,
                              release) and stale records forgotten; step 10b: the trim at a 1 MB cap
                              keeps a summary, a first-time line and an error with its stack through
                              2 MB of verbose lines, the ~ mark, VerboseWants loses no count; step
                              11 (Program.Copies.cs, LAST): two real SubModule instances - the second
                              stands down (no log line, model, mission, message), the first registers
                              ONE decorator of each kind and reports once; step 20b (Program.AttackRate.cs):
                              a slower swing through the real logic keeps the full-speed pause; step 21
                              (Program.BattlePace.cs): the stand-in weapon read for EVERY logic (step 3 - a native
                              read on a fake agent would crash), the older steps pinned at 0 (BattlePaceOff), each
                              class through the real logic (a fresh shield man 0.78 s, a tired one, a two-hander,
                              riders, bows fresh / tired / mounted, a crossbow, a javelin, you never), hot swap,
                              the master switch (its own step lifts a fresh man's share too), the summary; and the
                              cost - 1000 men held at once, the AI timer's tick ~0.005 ms, 0 bytes; step 23
                              (Program.Brace.cs): the stand-in brace body for EVERY logic (step 4), the real logic -
                              charge 20 → 40, hold 60 → 80, the AI's advance, an order change both ways, a wounded
                              man 30 → his cap 50, the player never, a man taken over, the input frame (ranged let
                              through, the shield up), the shield steps, the timer and the brace never lifting each
                              other, the guard, live floors / margins, leaving the field, the switches, the summary;
                              the cost - 1000 men looked at, 0.003 ms a tick, 0 bytes; step 28 (BraceNoFormationAndFloorRoll):
                              a no-formation man braces at 20 ± his own floor roll (not 60), his floor dice apart from the
                              margin's, both sliders live, the floor lines (step 23's checks pin BraceFloorSpreadPercent 0 -
                              BraceDefaults); step 24
                              (Program.Ready.cs): the ready count through the real strip and ALT views -
                              texts / colours, the deeper cell and the lifted bottom card, the panel rows, new
                              stats, the thresholds / BraceEnabled / ShowReadyCount live, the master switch, the
                              lines (the older strip / ALT steps pin ShowReadyCount off); step 25
                              (Program.Damage.cs, DamageReductionThroughDecorator): the real decorator - after
                              the roll (100 → 37.5..112.5, mean 75), the roll off still reduced on every kind,
                              never a fall / object / 0 hit, never below 1, live, the master switch, the lines
                              (the older damage steps pin DamageReductionPercent 0 - DamageDefaults); step 30
                              (Program.TiredBlock.cs): the real decorator + logic - DrivenProperty 40, the game's
                              chance untouched fresh, f 0.52 over −1.10 → 0.48, own 0.2 → 0.584, empty → 1, no
                              compounding, never you, the slider live (50 / 0), AthleticsEnabled / the master switch,
                              a braced man's own guard direction untouched by the input frame, the guard by f band
                              (step 34: split by a shield in hand - the stand-in read, a shield dropped mid-fight),
                              the [guard] and [summary] lines; the cost - 1000 tired men block + recompute, ~0.1 µs,
                              0 bytes (the older steps pin TiredBlockRandomPercent 0 - AthleticsDefaults);
                              step 32 (Program.SwingStack.cs): the real logic - your burst of stored swings, the hold from
                              the last one's release, the countdown to the next, the kick key refused at 0, hold-to-attack,
                              saving up; the AI by input - no hold from the stack, his own gap not refilling, at 0 held
                              exactly for the rest, his kick bit taken out; kicks spend one; the switches; the summary; the
                              recovery bar's "Swings 4/8" through the real view; the cost - 1000 men waiting, 0.09 ms a tick,
                              0 bytes (the older steps pin SwingStackEnabled off - SwingStackOff in AthleticsDefaults); step 33
                              (Program.Sprint.cs): every SprintKey name = a game InputKey (LeftControl 29); the real logic with a
                              stand-in ISprintBody - your key (press / release / refusals / a menu / a horse / the AI driving you),
                              the real decorator putting MaxSpeedMultiplier ABOVE 1 (0.80 x 1.6), no refill while sprinting, the
                              profile in 0.05 steps, drain only moving, the refill rates, the AI on new orders (charge / advance /
                              hold, melee, a rider, a man you drive), SprintAiEnabled and the master switch, left the field, the
                              fresh start, the clamp, the summary; the cost - 1000 men on a charge, 0.006 ms a tick, 0 bytes, ~12
                              recomputes a man; the sprint bar prefab and view (the older steps pin SprintEnabled off - SprintOff
                              in AthleticsDefaults; step 33's own checks pin SprintMinSeconds / SprintTopSpeedReachSeconds 0 -
                              SprintDefaults); step 35 (SprintTuningThroughTheLogic): an EXHAUSTED you through the real logic and
                              decorator - the run property 0.8 x 2.0 at the start (not 0.8 x 0.3 x 2.0), the fade towards 0.33,
                              TopSpeedReachDuration 2.6 → 1.0 and back, the floor's refill, reach 0 / live, the master switch,
                              an AI man off the floor, SprintMinSeconds 0 refused, the lines; step 36 / 37 (SprintByOrderThroughTheLogic):
                              shock troops - a move 1 s down to the 80% reserve, a second nothing (reserve kept), the charge
                              sprints the 80% left, an advance 2.5 s, the reserve live, the master switch, the summary; step 41
                              (Program.Wall.cs): the stand-in wall body for EVERY logic (step 4); the game's side - v1.4.8's Agent
                              has the private readonly Timer, the real Timer driven as vanilla drives it moves its StartTime exactly
                              on vanilla's update frames (6 in 3 s), our defensive set = GetArrangementOrderDefensiveness's; the
                              real logic - one man held / left per reason, the rewrite in the next pre-tick after vanilla's update,
                              the fallback every 0.1 s, a charge / a retreat let go with vanilla's value, the escape, the player
                              taking a man, the samples ON vs OFF, the switch and the master switch live, leaving the field, the
                              summary; the cost - 1000 men held, 0.016 ms a pre-tick, 0 bytes; the master-switch step lets held
                              men go (step 41's checks pin ShieldWallStepOutMetres 0 - WallDefaults); step 42 (Program.Leash.cs):
                              the real logic with men walking out at 2 m/s and back at 1 m/s - a shield wall's man and a square's
                              side man freed (nothing written), no rewrite at vanilla's update while out, reined within the 0.1 s
                              look (1.03 m at L 1), no new step before back in the slot, enemy gone, L 0 live (held, then not one
                              enemy read) and 2 live, the master switch, the lines and the summary; L 0 = step 41's exact writes
                              with and without an enemy in front; the cost - 1000 men, 200 out, 0.015 ms a pre-tick, 0 bytes; step 43
                              (Program.Fever.cs): aggression and the fever through the real decorator and logic - the share by the bar
                              (0.144 → 0.086 full, 0.058 at a half bar), never you or a rider, a charge armed at the absolute 0.8
                              peak, nothing burned before contact, the burn and the fade (0.75 at 7 of 10 s), the shield taken out
                              (4.9 s for 7 s of store), back to 0.6 after 5 recomputes, the proof's groups, the fever sprint (refusals,
                              the reserve, the roll a second), re-arming, 100 / 100 and FeverEnabled live, the lines and the summary;
                              the cost - 1000 men in a fever, 0.005 ms a tick, 0 bytes; the master-switch step ends a fever and
                              gives the game's values back (the older steps pin the share 100 / 100 and the fever off - FeverOff); step 44
                              (Program.FeverMarks.cs): the prefab; the real FeverMarkView over the REAL logic's fevers (TryGetFever and
                              the walk) with a stand-in camera (FakeFeverMarks: a pinhole at the origin) - vanilla's flag and the copied
                              rule, armed orange full / burning 0.7 / a faint yellow sliver / gone when faded, calm men, you, behind,
                              too close, off the screen, not placed, the enemy switch, the cap = the nearest on the screen, the fade at
                              5 m, 60% at 60 m, the height / length live, UI scale 4/3, no camera, FeverEnabled off, a hold, the gates,
                              the lines and the summary; the fail safe; the cost - 1000 men, 400 in a fever, the cap 200, 0.03 ms a frame,
                              0 bytes; the master-switch step takes the marks away and back (step 44's checks pin ShowAthleticsMarks
                              off - FeverMarkDefaults); step 46 (Program.AthleticsMarks.cs): the real view over the REAL logic's men
                              - every man but you a bar coloured green / blue / yellow / orange / red by his bar, the wound cap, a rider
                              1 m higher, both bars for the men in a fever, no jump when a fever ends, the enemy switch, the fever bars
                              off (the bars drop onto the point) / the Athletics bars off (step 44 exactly), the cap = the nearest on the
                              screen (an off-screen man nearer takes no slot, 4 projections), gap / length / bands live, the fade,
                              FeverEnabled off keeps them, nothing to draw, both switches, Athletics off, vanilla's flag, the lines and
                              the summary; the fail safe mid-walk; the cost - 1000 men ALL on the screen, 400 in a fever, the cap 200,
                              0.07 ms a frame, 0 bytes, 200 projections a frame; the master-switch step takes a calm man's bar away and
                              back; step 47 (Program.Impact.cs): the stand-in impact body for EVERY logic (step 6); the game's side - the
                              act_stagger_* fields of ActionIndexCache (by reflection: its static constructor asks the engine), Core's
                              codes = ActionCodeType / CombatCollisionResult, MissionTickAction.DropItem, the blow flags, StunPeriodMax
                              raised and restored, the engine calls' signatures; THE SPIKE through the real damage decorator and logic -
                              kicks 1-8 rotating (4 falls, the reel 0.42 → 0.83 s and the fall-and-rise 1.5 → 3.0 s at 0.5, slowed again on
                              a _continue), a missing fall, a rider, a 0-damage kick, a shield dropped and picked up / one never dropped, a
                              stagger (accepted, 0.9 s, a block and a hit from inside) and our blow (a Fall), a rider's block refused with
                              no engine call, a stagger the engine refuses; the stun x3 for an even blocker through the real decorator
                              (0.21 → 0.63, the cap 2 s, an odd one's 0.9 clamped to the game's 0.8) from worker threads, the recoil and
                              guard x3 vs the control; ImpactSpike off / on live, a watched man leaving, all six WORKS in the summary; the
                              fail safe (five levers disabled one by one by their errors, lever 1 still on, each [error] once, FAILS); the
                              cost - 1000 men, 10 blocks a tick, 0.001 ms a tick, 0 bytes; the master-switch step turns the spike vanilla
                              at once and back (103 steps; the config checks work for any tuned default)
                              TRAX_SMOKE_KEEP=1 keeps its temp folder + log to read
tools/DefaultsTool/           defaults.json upkeep: refresh (rewrite comments/order, keep every
                              value) | check; --file <path> for an exported one
```

## Build & deploy

```powershell
dotnet build -c Release
dotnet test  -c Release
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1
```

Game path and MCM path in `Directory.Build.props`; personal overrides in
`Directory.Build.props.user` (git-ignored). The deploy runs the AssemblyGuard and the offline
smoke test before it installs anything (`-SkipSmoke` only if the smoke cannot run on a
machine). The deploy fails while the game runs (DLL lock) — say so and hand Anton the deploy
line.

## Release

Live: Workshop item **3817259431** (Public, v1.1.1 since 2026-10-14 - v1.1.0 2026-10-12, v1.0.0 2026-10-10), GitHub releases v1.0.0, v1.1.0 and v1.1.1 with the zips.
The whole loop is **`tools/WORKSHOP-UPLOAD.md`** — read it before any release. **Publishing is
Anton's decision, after his playtest**: Claude never runs the uploader, and never posts or
uploads anything, without his yes for that release. **The release rhythm** (the sibling's, Anton
2026.07.28): fixes collect in `main`, versions do not — `module/SubModule.xml`'s `<Version>` (the
version's only home) is stamped ONCE, on release day, and committed BEFORE
`powershell -ExecutionPolicy Bypass -File tools\package.ps1` (the DLL carries the commit it was
built from). Then the uploader with `tools\WorkshopCreate.xml` the first time (never again),
`tools\WorkshopUpdate.xml` after. The dev and the release copy may both be enabled: the first
to load runs, the other stands down (Core `SingleCopy`).

**Editing text files: use the Edit/Write tools, never PowerShell `Get-Content`/`Set-Content`.**
Windows PowerShell 5.1 reads BOM-less UTF-8 as ANSI and writes it back as mojibake (every
—, →, ± in these docs was mangled once, 2026-09-27). **Commit messages**: PowerShell 5.1
splits a message containing double quotes into separate arguments — write it to a file and
`git commit -F <file>`, or commit from the Bash tool with a heredoc.

## References

- **Decompiled game, this exact version**: `..\reference\game-decompiled\` (CampaignSystem,
  SandBox, MountAndBlade, MountAndBlade.View, NavalDLC…). Missing assemblies (e.g. the
  GauntletUI / ViewModelCollection ones for HUD work) can be decompiled beside them:
  `ilspycmd -p -o <out> <dll>` with `$env:DOTNET_ROLL_FORWARD='LatestMajor'`.
- **Sibling mods** for proven patterns: `..\TrainingBattlesMod` (CLAUDE.md "footguns" list,
  model decorators, Gauntlet windows, deploy/package tools, AssemblyGuard, Workshop upload
  loop in `tools/WORKSHOP-UPLOAD.md`), `..\ImmersiveAI` (MCM bridge, mission views).
- Game install: `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord`.
  Workshop mods (MCM = 2859238197, maybe RCM) under `...\steamapps\workshop\content\261550\`.

# Done

- [x] **Step 1 — repo, docs, design spec, public GitHub.** Anton's ask of 2026-09-27 turned
  into `docs/DESIGN.md` (behavior + parameter table + the interpretations chosen where the
  ask was ambiguous: a "blow" is any attack, landed or not, `CostOnMiss` toggles it; hero
  multiplier 0.75 — the ask said both 0.75 and 0.5, the leader formula 0.75×0.75×10 settled
  it; party leader = the hero leading the fighter's own party; only blows drain; exhaustion
  is a cliff at 0). `CLAUDE.md` sets the manager protocol Anton asked for: the main session
  manages, ONE agent per step, sequential, every step ends built + tested + documented +
  committed + pushed so a session that runs out of tokens loses nothing. Conventions copied
  from `..\TrainingBattlesMod` (TASKS_TODO / AI_NOTES / TASKS_DONE, Core+Module split,
  decorate-don't-subclass models) plus its most expensive lesson made a hard rule from day
  one: MCM types only in method bodies or a satellite, so the mod loads without MCM. Game
  version pinned: v1.4.8. Public repo: github.com/TraxData313/trax_combat_enhancements.
  (2026.09.27 13.08.58)
- [x] **Step 2 — research.** Every hook verified in the decompiled v1.4.8 game and written
  to `docs/RESEARCH.md` (A–I + J hot swap + K logging, Anton's mid-step additions). The
  verdict: **no Harmony, no UIExtenderEx** — the mod needs only public API. Damage: every
  hit (melee, missile, area, charge, fall) funnels through `Mission.GetAttackCollisionResults`
  → armor first, then the non-virtual `AgentApplyDamageModel.CalculateDamage`, whose last
  step `ApplyGeneralDamageModifiers` is where a DECORATOR multiplies the roll; `AddModel<T>`
  hands us `BaseModel` in both campaign and custom battle (`MBGameManager.OnGameStart` runs
  every submodule before building `MissionGameModels`). Speed: a second decorator over
  `AgentStatCalculateModel` scales swing / thrust-and-bow-draw / reload; every game recompute
  (`Agent.UpdateAgentProperties`) passes through it, so the ×0.2 survives weapon switches —
  but the engine clamp is UNVERIFIED, and decorating that model swallows the tournament
  `SetAILevelMultiplier` (non-virtual, private field; War Sails already has the same bug) —
  fix noted. Blows: ranged has `OnAgentShootMissile`; a melee swing has NO event, so poll
  `GetCurrentActionType(1)` for the edge into `ReleaseMelee` (vanilla polls action stages
  per agent itself); landed incl. blocks/parries = `OnMeleeHit`/`OnMissileHit`. Leader:
  `Origin.BattleCombatant as PartyBase` → `LeaderHero` (NOT `General`, which is the ARMY
  leader); custom battle only sets a General on the player's side. Surprise: a MissionView
  added in `OnMissionBehaviorInitialize` is never registered with the MissionScreen (it
  registers views before `AfterStart`) — add views with `MissionScreen.AddMissionView` on
  the first tick; `MissionBattleUIBaseView` gives the create/destroy-on-toggle pattern
  hot swap needs. Surprise two: vanilla HUD and order movies are auto-generated code, so the
  orders-menu cards cannot be extended without UIExtenderEx → recommended our own panel
  while the order menu is open (Anton decides). MCM: fluent builder + `ProxyRef` verified in
  MCM 5.12.3 — edits hit our setters immediately, Cancel undoes through them, Done raises
  `SAVE_TRIGGERED`; format `"none"` keeps config.json the only store; so every parameter can
  be live. Newtonsoft 13.0.1 ships with the game and reads `//` comments (tested). "RCM" is
  not installed — RBM is (its posture/stamina bars are the style reference; playtest with RBM
  off). 13 design implications + 7 proposed parameters await Anton at the top of RESEARCH;
  AI_NOTES steps 3–9 hold the build pointers. (2026.09.27 13.36.27)
- [x] **Step 3 — scaffold.** The mod loads and does nothing yet — on purpose: every later
  feature plugs into a frame that is already proven. Core (netstandard2.0, no game refs):
  `SettingsSchema` declares all 32 DESIGN settings once (range, MCM group, plain-words
  description, all Live) and everything — file, MCM page, log, tests — walks it;
  `TraxSettings.Shared` is the one live object read AT USE TIME (typed property per key,
  `Set(key, value, source)` → `[config] X: old → new (source: …)`, `Version`, `Changed`);
  `ConfigFile` writes config.json with a header and a `//` explanation + default/range above
  every key and reads it tolerantly (comments, trailing commas, any casing, "0,75"); the
  FILE-REWRITE RULE (`ConfigMerge`): at every write the disk file is re-read — MCM wins only
  for the keys it changed since the last write (`EditTracker`, so moved-and-back or Cancel
  never counts), the disk wins for the rest, so a hand edit made while the game runs is never
  lost; unknown keys are carried along, a broken file is backed up first. 53 xUnit tests, one
  parsing DESIGN.md's table so doc and code cannot drift. Module (net472,
  `TraxCombatEnhancements.dll`): config.json + `trax_combat.log` in
  `Configs\TraxCombatEnhancements\` via `EngineFilePaths.ConfigsPath`; file created on first
  run, re-read at startup, every game start/load and every mission start; the log is tagged,
  2 MB-trimmed, verbose lines only with `VerboseLogging` and rate-limited, errors with stack
  (rate-limited, red in-game line); MCM page via the FLUENT builder, `ProxyRef` per setting
  (live), format "none", Done → file. Two findings corrected RESEARCH §H: a lambda with an
  MCM-typed parameter compiles to a cached static FIELD typed on MCM — the very load trap
  (proven with a probe DLL; McmBridge passes `Action<object>` instance methods instead), and
  MCM's "default" preset must stay because MCM.UI's Reset buttons apply it — we fill it with
  DESIGN's defaults before `BuildAsGlobal` (first value wins). Both model DECORATORS
  registered in `OnGameStart` as pure pass-throughs (every abstract AND virtual member
  forwarded) + the tournament `SetAILevelMultiplier` fix via a compiled private-field read;
  `EnduranceLogic` in every SP mission logs start/first tick/deployment and a `[summary]`
  block (agents built/removed/left, errors). RBM → `[compat]` + one yellow message (manager's
  f7dc70c). Tools: AssemblyGuard (MCM, Harmony, ButterLib, UIExtenderEx, NavalDLC,
  CustomBattle = hard fail; 0 errors, 0 warnings); OfflineSmoke — 16 checks on the real DLL
  with the game's DLLs on .NET Framework, no game: GetTypes() with MCM refused, every config
  flow, the tournament fix, then the MCM page built by MCM 5.12.3's own builder (32 settings,
  types, ranges, hints, groups), live sliders, Reset, Done; `deploy.ps1` runs build → guard →
  smoke → install as `TraxCombatEnhancements.Dev` (deployed; game was not running).
  UNVERIFIED in game (AI_NOTES Step 3): launcher load with/without MCM, MCM timing and
  mid-battle menu, decorators' base models, summary on every ending. PLAYTEST §1 has the lines
  that prove each. (2026.09.27 14.13.58)
- [x] **Step 4 — damage randomness.** Every landed hit now deals the game's damage × a fresh
  factor drawn uniformly from [1 − p, 1 + p) (p = `DamageRandomPercent`, default 50). Core
  (pure, tested): `DamageRoll` — the skip rules as one decision (object / no victim / fall /
  a hit the game shows as 0, then master switch, spread 0, shield and mount toggles, melee and
  ranged toggles — target and attack toggles combine), the game's own banker's rounding (0
  stays 0, a positive hit never below 1); `ThreadSafeRandom` ([ThreadStatic] per-thread dice,
  `IRandomSource` injectable); `DamageStats` — per-mission rolls by kind (melee / ranged /
  mounts / shields), min/avg/max factor, damage before → after, a 10-slice dice histogram,
  every skip by reason, errors per site, off-main-thread count, and the `[summary]` text. 39
  new tests (92), incl. a 200k-roll distribution check. Module: `TraxDamageModel` overrides
  ONLY `ApplyGeneralDamageModifiers` — BaseModel first (outside our try), then
  `DamageRandomizer` rolls on its result with settings read live per hit (MCM mid-battle →
  next hit); any exception in our part returns the game's value, the first per site per
  mission is logged with its stack, the rest counted. Logs: `[damage] mission start` (settings
  in effect), an always-on `first roll this mission` line naming the thread, verbose roll and
  skip lines (skips in their own rate-limit bucket — new `TraxLog.Verbose(tag, msg, bucket)`),
  and the `[summary]` damage block (EnduranceLogic resets at AfterStart). Thread question
  closed from source: all hit callbacks are `[MBCallback(null, false)]` = main thread
  (RESEARCH §A); the log proves it in game. OfflineSmoke +6 checks (22): the real decorator
  over the game's `CustomAgentApplyDamageModel`, fed hits built with the game's public
  `AttackCollisionData.GetAttackCollisionDataForDebugPurpose` — bounds, mean, all four kinds,
  every skip, hot swap, fail safe, log + summary lines, off-thread detection
  (`TRAX_SMOKE_KEEP=1` keeps its log). PLAYTEST §2 written (same blow different numbers,
  shoot, horse + charge, shields, spread 0 and ranged off mid-battle, falls/objects, summary).
  No new parameters, DESIGN unchanged. UNVERIFIED in game (AI_NOTES Step 4): the engine
  calling the hook in every battle type, shield HP following the roll, object hits, name
  wording. (2026.09.27 14.34.04)
- [x] **Step 5 — endurance core.** DESIGN §2 as written. Core (pure, 30 new tests → 122):
  `EnduranceRules` read live; `Fighter` stores endurance as a FRACTION of its pool (a
  MaxEndurance change keeps everyone's share by construction); `EnduranceMath` has ONE
  function per rule — `PoolPoints`, `BlowCostPoints` (10 / 7.5 hero / 5.6 party leader),
  `RegenFractionPerSecond(speed, topSpeed)`, `AttackSpeedMultiplier` (a float), `IsExhausted` —
  so endurance v2 (5c, DESIGN §2b) reshapes rules, not plumbing; `Charge` (cliff at 0, exact
  10 blows for a soldier, 18 for a leader), `Regen` (3 s delay with partial steps, 60 s standing
  / 120 s moving, recovery threshold checked every step); `MeanStd` (Welford, population std)
  + `FormationEnduranceStats` for steps 8-9; `IntervalStats` + `SpeedVerdict`; `EnduranceStats`
  + the summary text. Module: `EnduranceLogic` (lifecycle / Engine / Api / Log partials) +
  `TrackedAgent`: state by Agent.Index (reference-checked) + dense array; hero / leader flags at
  spawn (campaign: leads his own party; custom battle: the side's general, else its heroes);
  melee = per-tick poll of the channel-1 rising edge into ReleaseMelee, also checked inside
  `OnMeleeHit`; ranged = `OnAgentShootMissile` (0.1 s dedupe); landed-only mode keyed on the
  swing counter / the shooter's own missile indices (siege engines never charge); couched /
  braced = one blow when it lands; kicks, bashes, horse charges free; regen every 0.1 s for
  fighters below full, the horse's speed for riders. Speed: the float multiplier per fighter,
  `UpdateAgentProperties` only when it changes and only from the tick; `TraxAgentStatModel`
  scales swing / thrust-and-draw / reload after the base model (`SpeedPenalty`; verified the
  base models assign them fresh, so no compounding). Hot swap: EnduranceEnabled off refills
  everyone and lifts every penalty (decorator also checks it live), ExhaustedAttackSpeedPercent
  re-targets the exhausted. Read API for steps 6-9: `TryGetReading`, `TryGetFormationStats`
  (player team, mean ± std every FormationStatsRefreshSeconds), `FormationStatsVersion`. Logs
  built to replace the playtest we cannot run: the player's exhaustion / recovery / refill
  always; the first exhaustion's driven properties before → after → at recovery → restored;
  verbose per-blow and per-transition lines in own buckets; the summary block with blows by
  kind and riders, detection cross-checks (hits outside a counted release, polled ranged
  releases vs shots), free actions, heroes and leaders, the player, formations, regen standing
  vs moving, tick cost, walk/top speeds for 5c, and the ATTACK SPEED CHECK (median attack
  interval and clean swing length fresh vs exhausted → "ARE slower" / "NOT clearly slower …
  tell Claude") that settles the engine-clamp question. OfflineSmoke +6 (28): the real
  decorator and logic on uninitialized Agent objects (IL setters — reflection would run
  Agent's native static initializer). PLAYTEST §3 written. No new parameters, DESIGN
  unchanged. UNVERIFIED in game (AI_NOTES Step 5, each with its settling line): engine clamp,
  mounted swings as ReleaseMelee, poll cost, one shot event per shot, kick channel, couched
  hits, leader flags, stat model on top. (2026.09.27 15.12.26)
- [x] **Step 5b — Athletics rename + defaults.json (+ master switch).** RENAME (Anton: "it is
  the Athletics bar that gets depleted"): the pool/bar/points are "Athletics", the character-screen
  skill "the Athletics skill" - keys `EnduranceEnabled` → `AthleticsEnabled`, `MaxEndurance` →
  `MaxAthletics`, group "Athletics", MCM labels/hints, config comments, log tag `[athletics]`
  (buckets `athletics-*`, sites `athletics.*`, summary "Athletics …"), code (`AthleticsLogic` ×4
  partials, `AthleticsRules/Math/Reading/Stats`, `FormationAthleticsStats`, `Athletics.cs`, tests,
  smoke `Program.Athletics.cs`) by a verified script; docs by hand (DESIGN, CLAUDE, README,
  PLAYTEST, AI_NOTES, TASKS_TODO; RESEARCH keeps its words under a note). No aliases (nothing
  released). DEFAULTS.JSON (DESIGN §2c) - the ONE truth for default values: repo-root file,
  every setting with its value and its description + range as // lines; embedded in
  TraxCombat.Core.dll; each ParamDef takes its Default from it while SettingsSchema initialises
  (the schema has no default values; first-run config, missing-key fallback, MCM preset/Reset,
  hints, log all read ParamDef.Default). Fail safe: a bad embedded value → bottom of its range /
  clamped, listed in `DefaultsFile.Problems` → `[config] defaults.json PROBLEM` lines; `[config]
  defaults: … 33 keys for 33 settings` at load. `DefaultsFile.Check` (keys both ways, strict JSON
  types, ranges, comment layout with values blanked) + `tools/DefaultsTool` (refresh keeps every
  value, rewrites comments/order; check) - the tests print the refresh command. Unit tests now
  run on DESIGN's Default column (INITIAL values) via a module initializer, so tuning
  defaults.json never breaks them (proved by tuning three defaults: tests + smoke green); SchemaTests
  compare keys + types only; the smoke's config checks work for any default. config.json header:
  how to revert without MCM (delete a line / the file); a deleted config.json now means every
  default at the next re-read. MCM group "Defaults", verified against MCMv5 5.12.3 + MCM.UI 1.4.8:
  AddButton with ProxyRef<Action> (null setter; OnValueClick invokes it) - **Revert all to
  defaults** (live, each change `(source: defaults)`, config.json rewritten with every key, page
  re-reads via PropertyChanged, green message) and **Save current values as a defaults file**
  (defaults.json in the exact repo layout beside config.json, path + differing values logged and
  shown). MASTER SWITCH (manager's scope addition from Anton): `ModEnabled`, first setting in its
  own first group - off = vanilla at once: damage `Decide` → `ModOff` last (game value kept,
  recorded unrolled: "damage while the mod was OFF … avg N per hit"; the ON line gained "avg N
  per hit"), `AthleticsRules.Enabled` = ModEnabled && AthleticsEnabled (the switch-off path refills
  everyone and lifts penalties; the stat decorator checks it itself), back on = everyone full;
  logging stays on: `[mission] start … mod ON|OFF`, a `[mission] mod switched OFF … at 42.3 s`
  line per toggle, summary header "mod ON" / "mod OFF" / "mod was on for N% of the battle (…)"
  (Core `ModSwitchLog`). CLAUDE.md hard requirements: master switch first; defaults only in
  defaults.json. Tests 122 → 154 (DefaultsFileTests 21, MasterSwitchTests 11), OfflineSmoke 28 → 31
  (embedded defaults, master switch, both buttons clicked as MCM.UI does); 0 warnings;
  AssemblyGuard OK; deployed (game not running; installed folder has no stale files). PLAYTEST §1
  updated, §4 (ON/OFF A/B + flip mid-battle) and §5 (defaults: revert mid-battle, export, a
  changed default reaching the game) written. UNVERIFIED in game (AI_NOTES Step 5b): the buttons
  in MCM's real UI, the page refresh, messages over the options screen, toggle times, an OFF
  battle feeling vanilla. `git grep -i endurance` now hits only TASKS_DONE history, RESEARCH's
  findings (+ its note), and the deliberate "it used to be called endurance" mentions (DESIGN
  §2b, CLAUDE.md, the done step 5 lines in TASKS_TODO / AI_NOTES). (2026.09.27 15.53.36)

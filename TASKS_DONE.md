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
- [x] **Step 5c — Athletics v2.** Anton's additions built and DESIGN §2b folded into §2 (one
  current spec; a one-line §2b pointer stays for the 5d task line). POOL = the Athletics SKILL:
  max(`AthleticsPoolFloor` 50, `AthleticsPoolPerSkill` 1.0 × skill), never below 1, read once
  at spawn from `Character.GetSkillValue(DefaultSkills.Athletics)` (heroes their real skill;
  not the stat model's effective skill, so captain perks cannot wobble the bar), riders too.
  COST stays in points (10 / 7.5 / 5.6) against each fighter's own pool; state stays a fraction
  of the FULL pool, so pool-setting changes keep everyone's share and f = min(fraction ÷ peak%,
  1) needs no pool. PEAK ZONE (`AthleticsPeakPercent` 75): recruit 2 blows at full strength / 5
  to empty, legionary 4 / 13, Fian 5 / 17, a 300-skill leader 14 / 54 (DESIGN said 53 - it is
  53⅓ blows' worth, fixed). HEALTH CAP (`HealthCapsAthletics`): managed Health ÷ HealthLimit at
  every hit (`OnAgentHit`, after the drop; a killing blow is no cut) and every regen step; the
  peak line stays on the full pool. CURVES from f: the damage roll [1 − p, 1 + p × f] from the ATTACKER (`TryGetPeakShare`;
  a horse charge → the rider; untracked → full upside), attack speed S + (1 − S)f replacing the
  cliff, run speed M + (1 − M)f through `MaxSpeedMultiplier` only (CombatMaxSpeedMultiplier is a
  clamped share - scaling both would square it), horses via `MountSpeed` on the mount only when
  `MountMinSpeedMultiplier` < 1, found through the logic's own horse table so the decorator never
  calls native (every recompute from the tick, a released horse too). REGEN BY EFFORT: speed ÷ current top speed; ≤ `WalkEffortFraction` full rate,
  then a line to × `RegenMultiplierAtFullRun` 0.5. Walk ratio: human walk 1.8 m/s (monsters.xml,
  the formations' own walk gait), top inferred 6.2 × MaxSpeedMultiplier ≈ 3.9-4.9 m/s → walk/top
  ≈ 0.42 → 0.4 kept (at 0.42 the rate is 98%); the summary measures it. RECOMPUTES: 0.05 step
  per multiplier + exact end points + exact on reaching the top, ≤ 50 `UpdateAgentProperties` a
  tick. Retired `MaxAthletics`, `FullRegenSecondsMoving`, `MovingSpeedThreshold`,
  `ExhaustedRecoverPercent`; +9 settings (38; group "Tired fighters"); defaults.json refreshed.
  SELF-VERIFYING LOGS: summary pools (min/avg/max, at the floor, you, leaders), exhaustions +
  peak zone left/re-entered, fighter-time by f, health cap (cuts, biggest), regen by effort
  (walk vs faster seconds, avg rate, seconds per effort tenth), attack-speed checks binned by f
  with asked x and a verdict, run-speed checks by f (engine top ÷ fresh top vs asked, moving
  p90/max; horses "unaffected" at 1.0), walk vs run speeds, recomputes (+ held by the budget),
  damage upside by the attacker's f (+ rolls above their ceiling = 0); always-on YOU lines (pool,
  below / back at full strength, exhausted, off empty, refilled, wounded); verbose pool at spawn,
  health cuts, speed changes (fighters + horses). Tests 154 → 207 (every rule + DESIGN's blow
  counts + damage upside + binned checks); OfflineSmoke 31 → 33 (curve blow by blow, health cap,
  upside through the real damage decorator, horse table in the decorator); build 0 warnings,
  AssemblyGuard OK, deployed. PLAYTEST §3 rewritten (troop table, what to try, the lines that
  prove each); RESEARCH §C/§D addenda; CLAUDE Layout; AI_NOTES Step 5c + 5d/6-9 pointers.
  UNVERIFIED in game: skill read, the engine honouring mid-curve attack speeds, MaxSpeedMultiplier
  / MountSpeed live, the cap at the hit, effort units / walk ratio, horse-charge rider f,
  recompute cost at 1000 agents - each with its summary line in AI_NOTES. (2026.09.27 16.40.48)
- [x] **Step 5d — tired fighters step back.** The LITERAL spec, built on the engine's own
  scripted movement. RESEARCH (AI_NOTES "Step 5d"): the formation frame (rewritten every agent
  tick via `ParallelUpdateFormationMovement` → `SetFormationFrameEnabled`) and the scripted frame
  (`SetScriptedPosition[AndDirection]` → GoToPosition, until `DisableScriptedMovement`) are
  separate native states - the formation never overrides a scripted step and takes the man back
  when it ends; vanilla scripts formation men mid-battle itself (item pickup with NoAttack,
  ladder queues); `CanBeAssignedForScriptedMovement` is vanilla's own gate (detached, ladder,
  object, running away, already scripted) and vanilla systems check it, so nothing takes our man
  mid-step. Weighed: RBM's BackStep (decompiled: Harmony on `Formation.GetOrderPositionOfUnit` -
  needs Harmony, patches formation code), behaviour values (clean but "hang back", not a step -
  kept as THE fallback if backs turn), `SetMaximumSpeedLimit` (overwritten every tick), rank swap
  `SwitchUnitLocations` (rebuilds the unit list), vanilla's unused timed helper (its timer could
  cancel a later game job - our own timer instead). BUILT: when a counted melee swing ENDS, an
  AI fighter on foot in a field battle rolls `StepBackMaxChancePercent` × (1 − f) (f after the
  swing's cost; f 1 = 0%, no dice); a yes is queued and started from the tick (never in an engine
  callback): `SetScriptedPositionAndDirection` to the spot `StepBackDistance` straight away from
  his target, facing him, DoNotRun + NoAttack (`StepBackHoldAttacks`); released after
  `StepBackSeconds` (read live). SAFETY: player, riders, non-battle modes, tournaments/arena (by
  behaviour NAME), naval, teleporting, mission ending never roll; the tick refuses busy men
  (vanilla's gate), routing, shield wall/square/circle, retreat orders, no enemy within
  `StepBackEnemyRange`, a spot off the navmesh / >1 m height step / no straight way,
  `StepBackMaxAtOnce`, >20 starts a tick, the engine not taking it (then disabled at once).
  RELEASED on every path: time, order/arrangement/formation change, detach, player, mount, rout,
  switch-off (ModEnabled/AthleticsEnabled/StepBackEnabled: everyone at once, next tick), mission
  end (through the engine before the summary); left the field = no engine call; handed over to
  a game job (object, ladder queue) = never disabled; the game's reasons checked before "time
  up"; leftover flags of ours cleared by hand and counted. +7 settings (45, new group "Tired
  fighters step back"; the 4 planned rows + `StepBackEnemyRange` 4, `StepBackHoldAttacks`,
  `StepBackMaxAtOnce` 50), defaults.json refreshed, DESIGN §2 bullet = what was built +
  interpretation 11. SELF-VERIFYING LOGS: `[stepback]` mission start (rules + technique), mission
  kind, settings / on-off changes, the FIRST step back in full (f, chance, from → spot, enemy,
  facing before, formation + order, flags before → after; its end: moved, to the spot, mid-step
  and end facing, motion, hits blocked/landed, swings, flags after release), verbose per
  start/end/refusal; 8 `[summary]` lines - rolls by f band (peak row must be 0%), starts
  (holding / charging / no formation, most at once) + refusals by reason, ends (completed vs cut
  short by reason), moves (m, s), FACING at start / mid-step / end + motion, the GUARD (blocked %
  while stepping vs everyone else, swings while stepping), release checks (still scripted after
  release, overdue, at mission end - all must be 0). Core `StepBack.cs` + `StepBackStats.cs`, 19
  tests (207 → 226); module `AthleticsLogic.StepBack.cs` + `StepBackBody.cs` (IStepBackBody seam);
  OfflineSmoke 33 → 34 steps (the real bookkeeping with a stand-in engine side: rolls by f, queue,
  cap, live time, every release path, logs, summary; the master-switch step releases a running
  one). Build 0 warnings, tests green, AssemblyGuard OK, smoke OK, deployed. PLAYTEST §6 "Step
  back" (see it, force it, switch it off, where it must not happen, the summary, what counts as
  broken + the lines); CLAUDE Layout. UNVERIFIED in game: facing while scripted (THE risk), the
  engine taking it mid-melee, nothing lingering after release, the guard and NoAttack, real
  distance, navmesh checks on walls, tournament names, formations' shape at 50 at once - each
  with its summary line in AI_NOTES. (2026.09.27 17.15.26)
- [x] **Step 6 — player Athletics bar.** The mod's first HUD: bottom right under the vanilla
  health bar (right end on the hero bar's fill, below the horse bar - RBM's spot), one row: the
  word *Athletics*, `current / pool` (rounded UP: 0 only when truly empty), a slim bar - fill
  coloured by f (green at the peak line, blue just below, yellow / orange / red at or below
  `Bar{Yellow,Orange,Red}BelowPercent` 75/50/25 % of the line; most alarming wins, empty always
  red), a white marker at the peak line, the part the wounds hold dark red-brown, the rest dark
  grey; empty = *Exhausted*, word / number / frame red (no pulse: a 0.1 s refresh would stutter).
  REUSABLE PLUMBING for 7-9: `TraxHudView : MissionView` owns one GauntletLayer + movie + VM that
  exists exactly while Core's `HudGate` says so, read every frame (ModEnabled FIRST, Athletics,
  the view's Show switch, Hide Battle UI, photo mode, fight modes incl. stealth, the player on the
  field, the view's own condition) - live hot swap both ways; refresh every `HudRefreshSeconds`;
  suspend/resume; mission end; every entry wrapped - an error or a movie that does not load
  disables the view for the mission, removes the layer, logs [error] (the battle goes on).
  `IHudLayer` seam (`GauntletHudLayer` real: IsCustomType check, release the movie BEFORE
  RemoveLayer, a failed movie never goes on screen; the smoke's stand-in offline). Attached by
  `AthleticsLogic`'s first tick via `MissionScreen.AddMissionView` (`AthleticsLogic.Hud.cs`, one
  line per view). WHY THE DESIGN: Gauntlet's binding converts only strings (verified in the
  decompiled GauntletUI.Data/PrefabSystem), so every VM property has exactly the widget
  property's type; unknown attributes are silently ignored, so the smoke resolves every one;
  `FillBarWidget` draws shares, so no pixel maths and the UI scale applies by itself. Prefab
  `module\GUI\Prefabs\TraxPlayerAthleticsBar.xml` (13 widgets, native `BlankWhiteSquare_9` +
  `AgentHUD.Interaction.Text` only, no mouse events); deploy copies GUI (checked installed). +7
  settings (52): the 3 colour rows moved up from "Planned", 4 Advanced `PlayerBar*` (205 × 12 at
  62 / 54 UI px). Core `AthleticsBar.cs` (BarMath: band, shares, numbers, colours), `HudGate.cs`,
  `HudStats.cs` + 38 tests (226 → 264: bands on DESIGN's recruit - green, green, blue, yellow,
  orange, red - a wounded man never green, gate order, stats + summary text). SELF-VERIFYING
  LOGS: `[hud] attached:` (prefab installed?), `layer created … (was hidden: <reason>) - movie …
  loaded OK (N widgets)` / `FAILED to load`, `layer removed … - <reason>`, `not shown at … -
  <reason>`, `first values pushed` (number, fill, usable, colour, f, marker, size, place,
  thresholds), the FIRST time of each colour / EXHAUSTED / wounded per battle (verbose: every
  later colour change), `DISABLED` lines; `[summary] hud:` on screen s of s, builds, removals by
  reason, hidden time by reason, refreshes, errors + colours on screen (s and % per colour),
  colour changes, exhausted shown, wounded time / lowest usable. OfflineSmoke 34 → 37 steps: the
  prefab against the game's own widget types, properties, vanilla brushes/sprites and the VM's
  types (every VM property drawn); the real view driven by made-up frames - deployment hidden,
  built in battle, the five colours in order on 12 swings, wound, empty, refresh rate live, layout
  live, removed + rebuilt for all 7 reasons, stealth/tournament/duel, suspend, pause, mission end,
  summary via the logic; the fail safe (4 ways); the master-switch step removes the bar. Build 0
  warnings, tests green, AssemblyGuard OK, smoke OK, deployed. PLAYTEST §7 "Your Athletics bar";
  AI_NOTES step 6 (recipe for a new view, binding findings, gotchas); RESEARCH §G addendum +
  UNVERIFIED 12-13; DESIGN §3 as built + interpretation 12; CLAUDE Layout; README. UNVERIFIED in
  game: the drawing itself, placement at other resolutions / UI scales (4 live settings to tune),
  the War Sails steering state (vanilla bar drops 60 px). (2026.09.27 17.52.28)
- [x] **Step 5e — attack rate.** Anton: "attack speed" is the RATE - at m 0.5 one attack every
  2 s instead of every 1 s - so the whole cycle (wind-up, swing, recovery, the AI's pause) must
  follow m, not only the swing. RESEARCH (AI_NOTES "Step 5e", RESEARCH §C addendum): channel-1
  phases ReadyMelee → ReleaseMelee (swing + follow-through) → BlockedMelee (the attacker's recoil
  after a block/parry) → pause; ranged ReadyRanged (draw + aim) → ReleaseRanged/Throwing → Reload.
  Swing / thrust-or-ranged-ready scale the ready AND the release, ReloadSpeed the reload,
  HandlingMultiplier is the DEFENCE side (never touched), the block recoil has no visible property,
  no property sets the time between attacks. Per-agent action speed is NOT safe (SetCurrentActionSpeed
  is absolute with no getter, vanilla never uses it on combat actions, the native combat code
  paces them itself) - not built. The AI's pause lives in SetAiRelatedProperties (assigned with =
  every recompute): AIAttackOnDecideChance (vanilla's own "attack less" knob for defensive
  orders), AIAttackOnParryChance, AiShootFreq, AiWaitBeforeShootFactor. And NoAttack via
  SetScriptedFlags with no scripted position is vanilla's own "do not attack" (UseGameObject).
  BUILT: T1 the animations (unchanged); T2 `AttackRateAiDecisions` (on, A/B): the stat decorator
  scales the three chances x m and the aim ÷ m, re-applied to every tired fighter when switched;
  T3 `AttackRatePaceHold` (on, A/B): after each melee swing of a tired AI fighter on foot, NoAttack
  (guard up) until his next release can come no sooner than his fresh cycle ÷ m (his own
  release-to-release at m 1, else the mission's AI mean) - asked at the swing's end after the
  step-back roll, started from the tick behind `IPaceBody`, set only on a man with nothing of the
  game's on him, lifted on every path (time, a swing slipping through, switched off, left, player,
  mounted, mission end), under a game job waited out (a plain frame at most 3 s). Blocking
  untouched (handling, shield speed, every AI defence value, AIHoldingReady - a longer hold is a
  lowered guard); melee only (ranged: T1 + T2). SELF-VERIFYING LOGS: every channel-1 phase filed
  per f band (wind-up + held via the ready's progress, swing, clean swing, recoil, reload, pause),
  the cycle, m, target = the peak's cycle × the band's mean 1/m, measured ÷ target with "on target"
  (±15%) / "too fast" / "too slow" per band and a group verdict - melee / ranged x AI / you; left
  out: mixed bands, beyond 4 s ÷ m (12 ranged), cancelled readies, chains, cycles with a step back
  in them; the holds (reasons not held / not started / ended, NoAttack cleared by us / the game,
  the next ready after a hold); the guard by f (tired men must not block less); `[rate]` mission
  start, the FIRST slowed fighter's every touched value before → after checked against its factor,
  the FIRST hold in full. These replace step 5c's "attack speed check" lines (BinnedIntervals,
  SpeedVerdict, IntervalStats removed). Core `AttackRate.cs`, `AttackRateStats.cs` + 13 tests
  (264 → 274, 3 old interval tests gone with their classes); module `AthleticsLogic.AttackRate.cs`,
  `PaceBody.cs`, decorator + `SpeedPenalty.ScaleAiDecisions/AiSnapshot`; settings 52 → 54.
  OfflineSmoke 37 → 39 steps (the animations alone read too fast ~75% empty, the hold 100% on
  target, every hold path with a stand-in body, the first-slowed line through the real decorator,
  the master switch lifts a hold, step-back cycles left out). Build 0 warnings, tests green,
  AssemblyGuard OK, smoke OK, deployed. DESIGN §2 as built + interpretation 13; PLAYTEST 3n (arena
  duel, tired archer, you, a 3-battle A/B); CLAUDE Layout; README. UNVERIFIED in game (AI_NOTES
  5e #1-9): the engine honours the animation multipliers through ready and release; the native AI
  follows the four values; NoAttack holds swings in open melee with the guard kept; the recoil is
  unscaled (the known gap); the ready's progress reaches full. (2026.09.27 18.38.42)
- [x] **Step 9 — orders-menu strip.** Anton: "leave the strip under the orders cards - some vision
  of the state of the troops"; "below the arrows remaining, the Athletics state and the average
  health". RESEARCH (AI_NOTES "Step 9", RESEARCH §G addendum; generated OrderBar and RTS Camera
  Command System 5.3.38 decompiled beside the others): the "generated code" cards ARE real widgets,
  and reading them needs no patch - `MissionScreen.FindLayer<GauntletLayer>("MissionOrder")` →
  `UIContext.Root`; each slot holds two `OrderTroopItemBrushWidget`s (highlight + card; the transfer
  popup's stand alone - the filter); 16 cards in vanilla (keyboard columns TroopItem0-3 left / 4-7
  right, gamepad row), 8 with RTS Camera (one layout, columns bottom to top, clickable, the order
  icon always shown); slot k = FormationClass k (RefreshTroopItemBindings), and the card's public
  CurrentMemberCount (units minus the player, updated on OnUnitCountChanged) confirms it every
  frame; GlobalPosition / Size are screen pixels and our Scaled* bindings take pixels; vanilla's
  order icons hang 20 px under a card; at 1080p the bottom card leaves 24 px; the HUD font has ±
  but no ♥. TECHNIQUE (why): read the live cards - exact at any resolution / UI scale / layout and
  under RTS Camera's reversed columns - rather than copying the prefab maths, which RTS Camera
  already breaks. BUILT: `OrderStripView` (+ `OrderStripVM`, `Hud/OrderCards.cs` seams, prefab
  `TraxOrderStrip.xml`): while the menu is open, a card-wide cell under each drawn card - "72% ± 8"
  left and "HP 81%" right beside the game's icons, under them a 4-px bar (mean share of the men's
  own pools, coloured by their mean f with the player bar's bands, a translucent ± k·σ band via a
  FillBarWidget ChangeWidget, the peak tick); placed every frame, lifted if it would leave the
  screen; the player left out of the squad stats (as the cards) and health added to them. FALLBACK:
  a compact panel at the top centre (same numbers and bar per formation) for the rest of an open
  when no cards / not whole sets of 8 / two sets drawn / none drawn in 0.5 s / a card disagreeing
  for 1 s, or `OrderStripUnderCards` off; the next open tries the cards again. Settings 54 → 63:
  `ShowFormationHealth` (moved up from Planned), `OrderStripUnderCards`, `OrderStripTextSize` 13 /
  `TextOffset` 1 / `BarOffset` 20 / `BarHeight` 4 / `SideMargin` 2, `OrderPanelOffsetTop` 80 /
  `Width` 300; `ShowFormationSpread` / `FormationSpreadStdDevs` drive the band and the "± N".
  TraxHudView: `OnLayerFrame`, `QuietConditionToggles` (open/close verbose after the first build),
  `ViewConditionWhen`, `AddSummaryLines`; `HudFrame.OrderMenuOpen`; `HudStats.ConditionName`.
  GOTCHA: a widget's own @bindings resolve against its own DataSource and a list sets none - the
  first prefab hid the strip's visibility on the list; fixed, and the smoke's prefab walker (now
  generic: DataSource lists into ItemTemplates) fails such a binding (proved by breaking it). LOGS:
  `[hud] attached: orders strip …`; the first placement per mission (technique, cards with pixels
  and counts `(= formation)`, cells); `the cards changed` (rate-limited); every FALLBACK with its
  reason; `values at … (open #N, …)` once per open (verbose: each refresh); summary `hud: orders
  strip - opened Nx: under the cards in …, the compact panel in …; technique; card layouts seen;
  cells placed (lifted); card changes; short mismatches; re-scans; values pushed; fallbacks by
  reason`, and "health avg" in the Athletics formations line. Core `OrderStrip.cs` + 14 tests (274
  → 288); OfflineSmoke 39 → 42 steps (prefab, the real view with stand-in cards: vanilla / UI scale
  4/3 / gamepad row / RTS set / lift / live switches / every fallback / quiet reopen / summary;
  fail safe; the master switch removes the strip). Build 0 warnings, tests green, AssemblyGuard OK,
  smoke OK, deployed (TraxOrderStrip.xml installed). Docs: DESIGN §3.4 as built + interpretation 14,
  PLAYTEST 8 (8a-8g, what counts as broken), AI_NOTES, RESEARCH #18-20, CLAUDE Layout, README.
  UNVERIFIED in game: the live cards report the expected pixels and the cells land under them
  (`first placement under the cards … cards drawn … cells …`), RTS Camera's 8 cards match
  (`8 cards in 1 set`), the item-template lists and the ChangeWidget band draw, the numbers fit
  beside the icons, War Sails' naval cards. (2026.09.27 19.24.27)
- [x] **Step 10a — code review.** Fresh-eyes review of Core + Module (tests and tools as
  evidence), every doubt checked against the v1.4.8 decompile; the list is `docs/REVIEW.md` (R1-R24).
  TALLY: 0 blockers, 1 major (fixed), 9 minors (7 fixed, 2 deferred), 1 For Anton, 13 checked and
  not a bug. WORST FINDS: R1 (major) - at a tired AI swing's end the pace hold was skipped whenever
  a step back was merely PENDING, and the tick then refuses many of those (StepBackMaxAtOnce, shield
  wall, no enemy near) - at f 0 the roll always says yes, so past the 50 cap most tired men in a big
  battle were neither stepping back nor held (technique T3 silently off, the summary would read "too
  fast"); now the hold is queued anyway and TickPace (after TickStepBacks) drops it only for a man
  whose step back STARTED. R5 - a stale record at a reused agent index only left the loop: a running
  step back / pace hold stayed listed (engine calls on the old agent) and a slowed horse stayed
  registered; now one Forget() path for leaving / stale / deleted (+ an OnAgentDeleted backstop).
  R6 - the log did open/append/close per line on the main thread (~110 µs measured; with the
  playtest's VerboseLogging ~200 lines/s and a ~40 ms hitch at the first clash); now one AutoFlush
  handle (~7 µs, nothing lost on a crash), shared for reading, released at every mission end. Also:
  R2 a hold started while the last one waited was listed and counted twice; R3 teardown now always
  drops AthleticsLogic.Current (finally); R4 the summary header had no try (an exception lost the
  whole summary) and read agents' native side on the teardown fallback; R18 the first-tick sweep
  gave up on everyone after one bad agent; R19 a resumed mission takes the running-logic slot back.
  Verified fine (no change): views cannot tick on cleared agents (MissionScreen.OnEndMission
  unregisters them first), no MCM type outside method bodies, nothing in the save, the only event
  subscription is ConfigStore's app-lifetime one, no per-tick allocation, every stuck-state path,
  hot swap, DESIGN §1-§4 rules. DEFERRED: R8 verbose lines built before the limiter drops them (~30
  call sites, verbose-only cost); R21 the LATER features' do-nothing settings (step 10b). FOR ANTON
  (R7): with VerboseLogging on a big battle writes 30-50 KB/s, so the 2 MB cap trims every ~35 s and
  earlier battles' [summary] blocks are gone by hand-over - keep summary/error/mission lines through a
  trim (recommended), raise the cap, or play with verbose off; PLAYTEST (10b) should follow his pick.
  OfflineSmoke 42 → 44 steps (log writer; stale records) + R1/R2 checks in the attack-rate step - the
  R1/R2/R5 checks fail with the fix reverted (8 failures, proved); tests 288 (no Core logic changed);
  build 0 warnings, AssemblyGuard OK, smoke OK, deployed. Commits edff3dc, dc2fe8d, 0558f05, 0da7b9a
  + docs. (2026.09.27 19.56.57)
- [x] **Step 10b — polish.** Made the one playtest smooth and what the player sees clean; no
  balance number changed. (1) R21, no switch that does nothing: ShowTargetBar, TargetBarMaxDistance,
  TargetBarLingerSeconds (step 7) and ShowFormationBars, FormationBarsAlways, FormationBarHeight
  (step 8) had no reader - out of the schema, TraxSettings, defaults.json, MCM and config.json
  (63 → 57); their rows wait in DESIGN "Planned parameters" marked LATER (step 7/8), DESIGN §3
  items 2-3 marked LATER; no code existed only for them (two commented attach lines stay as
  pointers). The spread / health settings stay - the strip reads them. (2) R7, log survival (the
  manager's decision): Core LogTrim - a trim keeps EVERY non-verbose line (summaries, every
  feature's first-time / YOU / mission-start line, settings, MCM, mission, errors with their
  stacks) plus any [load] [compat] [config] [mcm] [mission] [summary] [error] line, and cuts only
  the oldest verbose lines at one point in time; verbose lines now carry "~" before the tag so the
  trim can tell them apart (the first-time lines share tags with verbose ones - a tag list alone
  would lose the strip's alignment proof two minutes into a big battle); last resort (kept lines
  alone over the target, many sessions) cuts the oldest kept; one note at the top. New setting
  LogMaxMegabytes (Advanced, 1-100, default 8, live; 58 settings); 8 MB trim measured ~50-90 ms; a
  failed trim retries after 1 MB. (3) R8 fixed too (the playtest runs verbose): RateLimiter.Peek +
  TraxLog.VerboseWants(bucket) - 25 call sites ask before building a line, a "no" counts as
  suppressed so the counts stay exact. (4) What the player reads: groups renamed and ordered
  (Refill, Your Athletics bar, Orders menu strip; the strip's switch first), MCM's Defaults buttons
  just above Advanced (MCM's UI sorts groups ASCENDING - verified in MBOptionScreen for 1.4.8;
  MCMv5's own sort is descending, a trap) with a smoke check of the page order; one vocabulary
  (Athletics / Athletics skill / peak line / empty), a unit in every label, every hint rewritten
  as a player reads it, MCM hints end "Applies at once, even mid-battle."; keys unchanged;
  defaults.json comments refreshed. (5) PLAYTEST rewritten as one ordered ~2 h session: A load
  without / with MCM, B master switch ON / OFF / flipped, C Athletics feel, D step back, E bar +
  strip (RTS Camera on; vanilla cards in A3), F campaign field battle + siege + tournament, G tuning
  + Save as a defaults file + Revert; VerboseLogging ON throughout; "What to send Claude"; appendix
  L1-L8 with every line and summary block; old section numbers mapped. (6) README (only what is
  built, MCM optional, RBM warned), DESIGN (§4 "What the player reads", "The log", interpretation
  15), CLAUDE layout, REVIEW R7/R8/R21 FIXED, AI_NOTES "Step 10b". BALANCE DOUBTS for Anton (not
  changed): recruits (floor 50, cost 10) are below full strength after 2 swings and empty after 5,
  so low-tier fights happen mostly tired; one attack in five at empty plus the pace hold may make
  tired melees crawl (30-40% worth a try); run x0.3 at empty lets nobody escape and strings out
  formations (0.5?); 45 s from empty to the peak line at rest means tired men rarely recover
  mid-fight (by design: the fresh take over); step-back chance 100% at empty may make the front
  "breathe" a lot. Tests 288 → 302 (LogTrim 11, Peek 3); build 0 warnings, AssemblyGuard OK, smoke
  44 steps OK, deployed. Commits 312a7ce, fb0164d, 72d94cb, b685f3c, 4f7452d + this. (2026.09.27 20.28.33)
- [x] **Step 11 — Steam packaging.** The release is ONE command + one uploader run away, and NOTHING
  was uploaded, posted or published: that is Anton's yes after his playtest. (1) `tools/package.ps1`
  (the sibling's loop, stricter): manifest gate (release Id TraxCombatEnhancements + name "Trax
  Combat Enhancements", vX.Y.Z) → build → unit tests → AssemblyGuard on BOTH DLLs, hard (MCM,
  Harmony, ButterLib, UIExtenderEx, War Sails, CustomBattle) → OfflineSmoke → DLL version = manifest
  → `dist\TraxCombatEnhancements` from scratch, checked against an explicit list (SubModule.xml, our
  2 DLLs + pdbs for line numbers in players' [error] stacks, GUI\Prefabs - the build output's
  Newtonsoft.Json.dll stays out) → `dist\TraxCombatEnhancements_v0.1.0.zip` (ZipArchive with `/`
  entries - PS 5.1's Compress-Archive writes `\`; an existing zip only with -Force, the release-rhythm
  guard) → file list + sizes: 7 files, 508,541 bytes; zip 210,539 bytes. (2) THE VERSION HAS ONE
  HOME: Directory.Build.props reads SubModule.xml's `<Version>` into both DLLs (an empty one fails the
  build), so "bump once" is one edit and the [load] line, launcher and zip agree. (3) DEV + RELEASE
  TOGETHER - found real, fixed: the game's Assembly.LoadFrom hands a second module with the same
  assembly identity the FIRST assembly and constructs a second SubModule instance, so both copies
  registered their decorators (two damage rolls per hit) and attached AthleticsLogic twice. Core
  `SingleCopy`: each SubModule claims an AppDomain data slot in OnSubModuleLoad (by INSTANCE - works
  for one shared assembly and for two); a refused copy sets `_inert` and returns from every hook
  before touching anything (not even the log, which the running copy holds open); the running copy
  logs `[compat]` at load and at the main menu and shows ONE yellow message; `[load] module: <Id>`
  (read from the SubModule.xml beside the DLL - a Workshop folder is a number) says which copy ran.
  Duplicate prefabs are silent in the game (MBDebugManager.Assert is empty; the later file wins).
  10 unit tests + smoke step 45 (two real SubModule instances: the second writes no line, registers
  no model, attaches nothing, shows nothing; the first registers ONE decorator of each kind and
  reports once) - mutation-checked: without the guard the smoke sees two damage decorators.
  (4) Workshop kit: `WorkshopCreate.xml` (creates the item Private; tags Utility / UI / Native /
  Singleplayer / v1.4.8 - Bannerlord has no "Gameplay" type; the PNG preview; a short pitch; "First
  release."), `WorkshopUpdate.xml` (ITEM_ID placeholder - the uploader fails on it before uploading
  anything), `WORKSHOP-UPLOAD.md` (the whole loop: playtest → version once, committed → package →
  create → record the id → paste the page → subscribe and check `module: TraxCombatEnhancements
  (the release)` → Public; updates; the preview; manual install; the uploader's quirks from a fresh
  decompile - new: Steam Cloud must be on for the account and the app, a comment directly under
  <Tasks> crashes it). (5) `STEAM-DESCRIPTION.bbcode`, for players: ±50% damage, the Athletics bar
  (skill-sized, floor 50, 10 a blow, heroes and leaders ×0.75 each, full strength in the top quarter,
  below it lucky hits / attack rate / run speed fall to one attack in five and a 30% run, wounds cap
  it, a minute's rest refills), tired men step back, your bar, the orders-menu strip; every number in
  MCM (optional) or config.json, live, master switch; NOT compatible with RBM; safe to add or remove
  mid-campaign (verified: no saveable types, campaign behaviours or Harmony in src); v1.4.8; an
  honest "first release" list (step backs, strip alignment, RTS Camera / War Sails, balance);
  feedback via GitHub issues. 4282 bytes of Steam's 8000, tags balanced. (6) Preview:
  `tools/preview_thumbnail.html` → `.png` (1024², 648 KB, headless Edge): the name over five
  Athletics bars fresh → spent in the mod's own colours, peak line, a wounded one; an in-game
  screenshot after the playtest would be better (noted). (7) README Requirements + Install, DESIGN §5
  (one copy runs, save-safe), PLAYTEST A1 `[load] module:` + A6 optional (the release package beside
  the dev copy - the only way to try the exact Workshop folder before uploading), CLAUDE layout +
  "Release", AI_NOTES "Step 11". FOR ANTON: the playtest, then the yes; the first public version
  (v0.1.0 or v1.0.0 - TASKS_TODO NOT DECIDED); paste the page and flip Public after the first
  upload; the test zip `dist\TraxCombatEnhancements_v0.1.0.zip` means a real v0.1.0 package needs
  -Force. Tests 302 → 312; build 0 warnings; smoke 45 steps OK; deployed. Commits 7c79099, ec93e4c,
  4a56f18 + this. (2026.09.27 20.50.39)
- [x] **Step 12 — playtest fixes, round 1.** Anton's first playtest log (20:58-21:14) found two bugs.
  (1) NO BAR IN THE TRAINING FIELD: it runs in the game's walk-about mode StartUp (Conversation while he
  talks), HudGate allowed Battle/Duel/Tournament/Stealth only - though Athletics ran there (37 blows,
  15 rolls). Now the PLAYER BAR has an outside-a-battle rule (Core HudGate + HudOutside, pure, tested;
  the orders strip keeps the fights-only gate): in StartUp only (towns, villages, the training field,
  arena practice - never Conversation/Barter/Deployment/CutScene/Replay), with the player on the field
  and tracked, while he holds a weapon or a shield (anything wielded in either hand - the game's own
  hands-empty test; fists only = no) OR his Athletics is below the top it can refill to
  (AthleticsReading.BelowFull - a wound's cap counts as full), plus a 1 s grace (a documented plumbing
  constant: a weapon switch empties both hands for a moment and would rebuild the layer). New setting
  ShowPlayerBarOutsideBattles (true, live; 59 settings; schema, TraxSettings, defaults.json +
  DefaultsTool refresh, DESIGN table + §3, MCM by the schema). New hide reasons OutsideBattlesOff /
  NotTracked / OutsideIdle, HudShow (why it is up); every build and removal outside a fight logged with
  its reason in its own rate bucket (hud-outside), the attach line tells the rule, the summary's hud:
  line gains "outside a battle: shown Nx (weapon drawn N, refilling N), on screen N s". (2) MCM RETRY
  LOOP: in the 21:08 session MCM's module was off but another mod carried an MCMv5 5.12.2 DLL, and the
  bridge retried "not ready" every second all session. MCM builds its services only in its own
  main-menu hook (5.12.3 decompile), so: Core McmPlan (tested) - the module list from the load line;
  DLL but module off = ONE line "MCM's module is not enabled - no settings page; config.json only." and
  no attempt; the first attempt at the main menu (the old pre-menu tick tries could never succeed);
  retries 1/s capped at McmPlan.MaxRetries = 30 (a documented constant, not a knob), then one give-up
  line. (3) EXIT HANG - no code, AI_NOTES "Step 12": we unload 4 s after the last mission and own no
  thread/task/timer/process (grep-verified), OnSubModuleUnloaded cannot block; session 2 (21:08) has no
  unload line at all; suspects another mod's helper process (ImmersiveAI.Dev) or Watchdog.exe;
  diagnosis = the process tree while it hangs - BUGS line kept open. PLAYTEST: A1/A4 MCM lines, E1,
  F4, new F6 (the training field and a town), L1/L7, 59 settings. Tests 312 → 323; build 0 warnings;
  AssemblyGuard OK; smoke 45 → 48 steps OK (the new HUD step mutation-checked: without the rule 8
  checks fail); deployed (the game had closed). Commits b211bb1, b20ba73 + this. (2026.09.27 21.35.29)

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

- [x] **Step 13 — PAUSE ONLY (Anton's playtest call).** The animation slow-down read as "slow-mo"
  ("I start swinging in slow-mo"), so every attack animation now plays at FULL speed and the whole
  slow-down is a NO-ATTACK TIMER: an attack of duration D (wind-up up to full + release; ranged + the
  reload after the loose, the timer from the reload's end) ending at m = S + (1 − S) × f leaves
  D × (1/m − 1) in which no new attack may START (under 0.1 s: none). RESEARCH (AI_NOTES "Step 13",
  RESEARCH §C addendum, written before coding): MissionMainAgentController writes the player's input
  into MovementFlags in OnPreMissionTick; behaviours pre-tick in REVERSE list order and a mod's logic is
  appended last (it would run before the controller), the native reads the flags right after the
  pre-tick - so YOUR hold is a tiny PlayerAttackGate added then moved to index 0 of
  Mission.MissionBehaviors: it pre-ticks right after the controller and clears only AttackMask while
  the hold is on (no wind-up at all; a HELD button passes the frame the hold ends = hold-to-attack;
  block bits, kicks (EventControlFlag.Kick), moving, weapon switches untouched; never during a ready -
  bits vanishing mid-ready would release the blow; no Harmony, nothing patched). Your hold begins at
  the release's START when its pause is sure to be ≥ 0.1 s (no chained blow past it), the countdown at
  the attack's end; presses during it are swallowed and counted and flash the bar; shield bashes wait
  (attack button while blocking), the reload is never held. THE AI: step 5e's pace hold became the AI
  timer - NoAttack for D × (1/m − 1) after EVERY attack, melee AND ranged, on foot AND mounted (the
  fresh-cycle reference, expected-ready maths and the rider refusal went). AttackRateAiDecisions
  default → OFF: Anton's log (21:06/21:14/21:26) read the tired melee AI 128% / 172% "too slow" and
  "the next ready came 1.5-2.6 s after a hold ended" - the hold already carried its target and the
  decision scaling + NoAttack's re-decision stacked on it; kept as the A/B. New settings (59 → 66):
  AttackRatePlayerTimer (your timer's switch - an escape hatch), AttackAnimationMinPercent (100 =
  full speed; the old animations x max(m, min%) optional), and - per Anton's mid-step change via the
  manager ("above that bar add a bar 'attack recovery' that empties when I attack and until it fills I
  can't attack; inside it add the secs delay added"), replacing the planned countdown text -
  ShowAttackRecoveryBar, FlashBarOnEarlyAttack, RecoveryBarWidth / Height / OffsetAbove: the Attack
  recovery bar (AttackRecoveryView + VM + TraxAttackRecoveryBar.xml, native sprites / brushes) just
  above the Athletics bar - empty at your attack, filling over the pause with "1.3 s" inside (tenths
  rounded up), full otherwise, two white pulses on an early press; shown with the Athletics bar and
  only while your timer is on. No room above the old row (vanilla horse bar 80 px, health frame 90 px
  up) → the Athletics row moved 54 → 30 px, the recovery row takes 54. Config FORMAT 2: a format-1
  config.json still holding the old defaults (AttackRateAiDecisions true, PlayerBarOffsetBottom 54) is
  migrated once and logged (Anton's config holds both). Core: AttackTimer.cs (AttackTimerMath,
  PlayerAttackTimer, AttackRecoveryReading), AttackRate / AttackRateStats reworked (animation asked per
  band, the timer rows - D, m, asked vs the measured gap, after its end, early starts - your timer's
  counters, the AI timer's kinds / mounted), ConfigFile.Migrate. Module: the phases build D and flag the
  attack's end; AttackEnded (after the step-back roll) → yours or the AI's; AthleticsLogic.PlayerTimer.cs
  (the gate's frame, releases on switch-off / not you / mission end, a tick safety net, the recovery
  read, [athletics] YOU lines - the first pause / press / end / held-button fire in full, the rest
  rate-limited); the stat decorator's animation floor; HUD views log in their own rate buckets now.
  Logs self-verifying per f band, melee / ranged, AI / you: animations asked (×1.00), phases vs the
  peak's, each timer vs the gap it left, attacks started inside a timer (must be 0), your swallowed
  presses and held-button fires, timers released at mission end; the verdict's target stays fresh ÷ m.
  Tests 323 → 333; build 0 warnings; smoke 48 → 51 steps (the attack rate rewritten: AI melee / ranged
  / throw / riders, every lift path, YOUR timer through the real logic; the gate first in a stand-in
  mission's list; the recovery prefab and view); deploy.ps1 green (guard OK, installed - the game was
  not running). Docs: DESIGN §2 (PAUSE ONLY folded in, the animations optional/legacy), §3 (the recovery
  bar), §4, interpretation 16, the table; PLAYTEST C1 / new C1b / C2 / C4-C7 / E1 / L1 / L5 / L7;
  CLAUDE layout; README. Commits 06ef514, d3f1dbe, 84e5ac0, 188510e + this. (2026.09.27 22.32.31)
- [x] **Step 15 — research: battle pacing (no code).** Anton: battles end fast; he wants them slower for
  tactics or stronger heroes, "like RBM but without the units overhaul", and "just let me see what is in
  there". Wrote `docs/BATTLE_PACING.md`: the menu of 11 levers (length with confidence, tactical feel,
  hero power, cost S/M/L, risk, Harmony, conflicts), a recommended package, the baseline from tonight's
  log, RBM technique by technique, the step back, our levers, compatibility with our features and RTS
  Camera Command System, the UNVERIFIED list. Decompiled RBM v4.5.0.2 (6 DLLs) and RTS Camera Command
  System to `..\reference\` (outside the repo; nothing copied). Findings: RBM's length comes from
  formation-level AI (a charge that keeps slots, charging men who want melee only within ~2 m, per-man
  "frontline" micro, culture battle plans advancing as formations to 75 m) and its armour rework — almost
  all Harmony into formation internals; its per-man AI values are not simply more defensive; no rotation.
  RBM's BackStep = a 0-0.3 m position lock (`Agent.SetTargetPosition`) re-picked every ~0.5 s in a
  charge, no direction set, so the combat AI keeps the man facing; ours turns because the scripted
  `GoToPosition` is navigation (faces the path, the direction applies on arrival - 2 of 2159 arrived).
  The public fix path: `AgentComponent.OnAIInputSet` (RTS Camera uses it) - write "backwards" into the
  AI's local input vector, clear only the attack bits, keep the guard; the same hook can replace
  `NoAttack` and keep the pause through a step back. The log: tired men are defenceless (blocked 2-13%
  held by the AI timer, 4-6% stepping back, vs 33-45%), 41% of landed melee hits in the 240v240 struck
  men in those states, Athletics empties within ~10 s of contact - so exhaustion currently shortens the
  melee. Recommended: #2 the guard really up (+ the step back fixed, the pause kept), #1 a troop-only
  damage scale (heroes exempt; start 0.75), #3 troop caution as an A/B, then #4 rank rotation or #5
  front ranks only. AI_NOTES "Step 15"; TASKS_TODO 15 checked. No code, settings or defaults changed.
  (2026.09.27 23.21.33)
- [x] **Step 14 — the run-speed floor 0.7 + the refill curve.** Anton after his 240v240 (asleep, no
  questions): "make them slow down to 70% speed" (0.3 was "too slow, unrealistic") and "recover faster
  when it's low and slower as it is fuller … not crazy, maybe half linear". (1) `MinMoveSpeedMultiplier`
  0.3 → 0.7 in defaults.json and DESIGN's initial value (a design change, like step 13's); config format
  3: a config.json of format ≤ 2 still holding the old default 0.3 gets 0.7 once, logged
  `[config] migrated config.json …: MinMoveSpeedMultiplier: 0.3 → 0.7 (format 2 → 3, …)` and rewritten as
  format 3 - dry-run on the real DLL against Anton's own file: that one value moves, the new key is added
  with its default, nothing else of his changes. (2) New setting `RegenRateNearFullPercent` (Refill, 10-100,
  50): the refill rate is a straight line in the fill x (share of the FULL pool), r0 × (1 − (1 − k) x) × the
  effort multiplier, r0 = ln(1/k) / ((1 − k) T) so empty → full at a walk still takes
  `FullRegenSecondsStanding`; 100 = the old flat rule to the bit. Each regen step is integrated exactly
  (Core `RefillFrom` / `RefillSeconds`), so the 0.1 s step never changes a refill time; the health cap still
  caps the target. At 50 / 60 s: half the bar ~25 s, the peak line ~41 s (was 45), the last quarter ~19 s
  (was 15). Schema + player-words hint, TraxSettings, AthleticsRules, defaults.json (+ refresh), DESIGN row
  + §2 text with the small table + interpretation 17, MCM by construction, live. Summary: the settings
  sentence and the regen line state the curve, new line `Athletics refill from empty to the peak line (no
  blow between): N runs, avg X s … - 40.7 s at a walk`, the YOU lines give the time up from empty. Tests
  333 → 351 (the migration; T for several k and T; DESIGN's table; k 100 bit-identical over 3000 random
  steps; boundaries; monotonic; step-length independence; the empty-to-peak report); build 0 warnings;
  smoke (51 steps) on DESIGN's 0.7 / 50; deploy.ps1 green (guard OK, installed - the game was not
  running). Docs: PLAYTEST A1 / C / C1 / C6 / L1 / L4, README, Steam page, CLAUDE layout, AI_NOTES
  "Step 14". Commits 3127fee, 9d2cbab, f1bf966 + this. (2026.09.27 23.39.43)
- [x] **Step 16 — the guard really up + a step back that faces the enemy** (BATTLE_PACING lever #2; Anton
  asleep, no questions). Why: the 240v240 log - held men blocked 2%, stepping-back men 5%, everyone else 33%
  (41% of landed hits struck those two states); 80% of step backs turned their backs, 0.6 m of 2 m, 2 of 2159
  arrived; at empty a started step back dropped the pause (R1's rule), so the empty band cycled at 2.2 s.
  Verified first (AI_NOTES "Step 16"): `Agent.OnAIInputSet` is a main-thread-only MBCallback that walks the
  components in add order; components may be added from our tick (the async agent tick starts after it);
  RTS Camera Command System adds one to every agent and turns the callback on for all. Built: ONE per-man
  `AiInputComponent` (added lazily at a man's first input hold, the callback turned off again when he is idle
  unless another component overrides the hook) with two wishes the logic writes each tick - (1) the AI timer
  by input (`AttackRatePaceByInput`): only the attack bits out, his defend bits / moves / event flags his own,
  a guard (`DefendDown`) raised when he wants to attack (`AiHoldRaiseGuard`), a ready always cancelled with a
  guard, never released; (2) the step back as a backpedal (`StepBackBackpedal`): 5d's gate and probe (the
  whole 2 m checked), then a backwards input along that line turned into his body frame every tick (his own
  Forward/Strafe bits out), ending at StepBackDistance covered (arrived), StepBackSeconds, the ground 0.6 m
  further back failing (edge ahead, every 0.25 s) or 5d's reasons. The timer SURVIVES a step back (R1's drop
  gone): by input both run to the later end; a NoAttack hold behind a scripted walk is deferred and set the
  tick it ends (else "covered"); R1's original bug cannot return (never skipped for a pending or running step).
  A/B: the two switches off = steps 13 / 5d exactly; read at each start, logged when switched; the master
  switch releases both at once. Never the player or a man he commands; RTS Camera coexists (ours runs last;
  its defensive hold acts only where nobody steps back). Logs: the header names the technique ("AI holds:
  Input / Legacy / mixed"); 4 new "AI holds" lines (techniques, the GUARD by state, the hook's calls / edits /
  never-called holds / errors, overlaps + deferrals); step back: every-0.25 s facing + "back turned at ANY
  sample", arrived, m/s; attack rate: stepped-back cycles now counted and shown apart, each band against the
  timer's floor D/m; the first held man and the first step back in full (input bits before → after, the
  vector, every 0.25 s); a WARNING if the engine never calls the hook. 70 settings; tests 351 → 367; smoke 51 →
  52 steps (Program.AiInput.cs + R1 re-checked); build 0 warnings; deploy.ps1 green (installed). Docs: DESIGN
  §2 + table + interpretation 18, PLAYTEST D (D4 = the A/B recipe) + L5 / L6 / new L6b, CLAUDE layout,
  AI_NOTES "Step 16" (decisions, UNVERIFIED with the lines), REVIEW R1 note, BATTLE_PACING, README, Steam page;
  TASKS_TODO 16 + three BUGS ticked. Commits a06e775, f0c2ea2, 21ea147, 64367fc + this. (2026.09.28 00.23.40)
- [x] 17. Second fresh-eyes review of steps 12-16 (`git diff 3a797da..HEAD -- src`, ~4000 lines) before Anton's
  morning playtest - docs/REVIEW.md R25-R36: 0 blockers, 0 majors, 4 minors (3 fixed, 1 deferred), 1 For Anton, 8
  checked and not a bug; every engine assumption re-checked in the v1.4.8 decompile and RTS Camera 5.3.38. FIXED:
  R25 - RTS Camera's free camera makes your hero AI-controlled while he stays MainAgent: his attacks started player
  pauses the gate never enforced, so the AI's next attack read as "the input gate did not stop it (tell Claude)"
  (the line PLAYTEST says must be 0) - now the pause is for YOUR hands only (`YouDrive`: none starts, a running one
  ends as "not you", no gate-miss or "started early" count); R26 - `RangedSiegeWeapon` fires on its pilot's attack
  bits, so a leftover pause swallowed a ballista's shots - the gate lets them through while you use a game object
  (the pause keeps counting); R27 - a backpedal the game took over skipped its release and left our OnAIInputSet
  callback on for that man - its release (sample, unhook) now runs on that path (the scripted walk's rule
  unchanged). DEFERRED R28 (the "never called" warning can be a stunned man: PLAYTEST L6b now reads it by the
  ratio). For Anton: your hero under the AI has no pause at all (leave it, or hold him like any AI man). Checked
  fine: the player's hold cannot outlive its attack; the gate's move to index 0 (only MissionMainAgentController
  writes MovementFlags); the component's lifecycle and RTS coexistence; DefendDown only ever replaces an attack
  wish; the backpedal's edges and time limit; R1 stays closed; the refill curve and migration maths; no per-tick
  allocation. Smoke extended (player timer: R25 / R26; AI holds E2: R27), mutation-checked (11 failures with the
  fixes reverted); tests 367 unchanged (no Core fix); build 0 warnings; deploy.ps1 green (installed). Docs: REVIEW,
  PLAYTEST C1b 5 + L6b, CLAUDE layout, AI_NOTES "Step 17 - review". Commits 61333b2, 1f099b3, 1935bea + this.
  (2026.09.28 00.49.14)
- [x] **Step 18 — kicks and shield bashes cost Athletics** (Anton, 2026-09-28: "3, a slider, hero/leader multipliers
  apply like any blow"). New `CostPerKickOrBash` (3, 0-20, 0 = free as before; Athletics group after `CostPerBlow`;
  defaults.json + refresh; MCM from the schema; Anton's config.json gains the key at its default on the next load - the
  existing "missing setting added" path, nothing migrated). A kick or bash costs 3 × the blow's multipliers (hero 2.25,
  party leader 1.69), ONCE, when it starts, landed or not (`CostOnMiss` stays a blow rule - DESIGN interpretation 19);
  paid like a blow otherwise (curves, peak line, exhaustion, the refill delay restarts) but NOT a blow: no attack timer,
  no step-back roll, never in `Blows` (step 13's rules stand: kicks never held, bashes wait while a pause runs). Core:
  `AthleticsMath.KickOrBashCostPoints` / `ChargeKickOrBash` (shared `Drain` with `Charge`; cost 0 = nothing at all),
  `KickBashTracker` (one decision per action: channel 1 and channel 0 OR-ed, the hit as the fallback, 1.0 s same-action
  window). WHY two channels: where a kick plays is unproven - the playtest log saw one bash on channel 1 and no kick ever,
  while the game's `StandingPoint` reads kicks on channel 0 - so the tick now also reads channel 0 on foot (one native call
  per fighter; the poll measured 0.077 ms at 480 men) and the summary says which channel saw each; drop the unused read
  after the playtest (AI_NOTES "Step 18"). The hit path reads both channels first, so a bash that lands is charged once and
  a kick no poll saw is still paid at its hit. Summary: the blows line counts kicks/bashes apart; "Athletics free" became
  `Athletics kicks/bashes charged N (P points; by riders R): … | seen starting: … (channel 1 a, channel 0 b) …`; the settings
  sentence prices both. Your kicks log a `[athletics] YOU: kick …` line (the first with the rule). Tests 376 (+9
  `KickBashTests`); smoke +1 step (costs, dedupe on every path, no timer / roll, 0 / off live, YOU line, action codes) and a
  kick in the master-switch step; build 0 warnings; deploy.ps1 green (installed). Docs: DESIGN §2 + table + 8 / 19,
  PLAYTEST C intro + C1c + L4, README, Steam description, CLAUDE layout, AI_NOTES "Step 18". Commit 2f29477 + this.
  (2026.09.28 08.08.51)
- [x] **Playtest round 3 read + the loading-screen version.** Anton's 200v200 infantry battle
  (08:0x, 368 s, 401 agents, 0 errors) proved step 16 in the real game: the backpedal kept
  87% of step-backs facing their enemy mid-step (12% before), 2% back turned (80% before),
  1.43 m of 2 m moved (0.6 before), 259 arrivals (2 before), and men blocked 45% while stepping
  back (5% before); men held by the AI timer blocked 52% (2-13% before) - now ABOVE the 30% of
  everyone else, the raise-guard press doing its job and maybe more (AiHoldRaiseGuard is the
  lever if tired men turtle). The ×0.70 run floor held at empty; the timer held (0-1 attacks
  started early in ~2400 holds - the older "fresh cycle ÷ m" verdict still reads "too fast" in
  the low bands because few start-to-start cycles stay inside one band there, the timer line is
  the real proof); tick cost 0.055 ms at 400 men. No kick or bash happened, so the step-18 cost
  is still unproven. His screenshot showed the loading line as "0.1.0+<40-char commit>":
  ShortVersion() strips the "+commit" for the in-game line only; the log keeps the full id
  (29976a7). (2026.09.28 08.26.28)
- [x] **Step 19 — the hideout boss fight is a fresh start for the player's side** (Anton, 2026-09-28: "they will come
  fresh and we will be tired"). New `HideoutBossFightRefill` (on, Refill group, live; defaults.json + refresh; MCM from
  the schema; 72 settings). The moment a hideout's boss fight BEGINS - duel or battle - every living fighter on the
  player's side refills at once to the top his wounds allow (`AthleticsMath.FreshStart`: health read first, the peak line
  still on the full pool) and whatever ran on him ends through its own path (a step back, an AI pause - running, queued or
  deferred -, your pause; new `FreshStart` end reasons, named in the summaries only when > 0), phases reset, speeds
  re-targeted exactly. WHY this hook (research first, AI_NOTES "Step 19"): both v1.4.8 hideout missions (the classic
  `HideoutMissionController` and the stealth `HideoutAmbushMissionController`) start SandBox's
  `DefeatHideoutBossObjective` ("hideout_mission_defeat_hideout_boss_objective") on the public `MissionObjectiveLogic`
  in the very call that starts the fight - for both choices - so the tick compares `CurrentObjective` by reference (no
  Harmony, no private state); duel vs battle from its name's raw `TextObject.Value` text id (any language). Who refills
  = the game's own teams then: in a duel the game moves your men to Team.Invalid, so only you refill, as Anton wants.
  The boss's side is spawned FRESH in the cutscene (new agents → full at spawn) - only measured, never touched; the
  line proves it in game ("all fresh" / "NOT all fresh - tell Claude"). The intro (CutScene mode in a hideout) is noted,
  so the summary says plainly when a boss phase played but the fight's start was never seen ("tell Claude"). Fail safe:
  the side reads come first - an exception = one `[error] hideout.refill`, NOTHING refilled, the fight goes on. Logs:
  `[athletics] hideout boss fight (duel|battle): refilled N of the player's side (you X → Y of P) at … s - …` always,
  `[summary] hideout boss phase (…): …` in hideouts. Tests 387 (+11 `HideoutBossFightTests`); smoke +1 step (the game's
  MissionObjective type, stand-in teams: battle / duel / off / fail safe / summaries) + the master switch step,
  mutation-checked (13 failures); build 0 warnings; deploy.ps1: build, AssemblyGuard and smoke green, the INSTALL was
  refused - the game was running (the launcher process holds the DLL); it installs with the next
  `powershell -ExecutionPolicy Bypass -File tools\deploy.ps1` after the game closes (29976a7's short version rides
  along). Docs: DESIGN §2 + table + 20, PLAYTEST F5 (two hideouts: duel once, battle once) + L4 + G3's 72, README,
  Steam description, CLAUDE summary + layout, AI_NOTES "Step 19". Commits c183e47, 12663d4 + this.
  (2026.09.28 09.35.08)
- [x] **Step 20 — hold ALT: Athletics + health under the game's formation markers** (Anton, 2026-09-28: "I see the
  Athletics and health numbers above the troops when I hold ALT - it now shows me the troop count and distance"; LATER #8
  built in its "only while vanilla shows the markers" form). Under every marker the game draws, centred just under its
  distance number: `72% ± 8` (the men's average Athletics ± spread, IN THE COLOUR of their average f - the bar's bands)
  and `HP 81%`, over the strip's slim bar (fill, ± band, peak tick). Yours and your allies' always; the ENEMY's too
  (`AltMarkersShowEnemy`, on - vanilla marks them; knowing the enemy is tired is the point). WHEN (research first,
  AI_NOTES "Step 20"): vanilla's `MissionGauntletFormationMarker` sets `IsEnabled = ShowIndicators key (5: Left Alt / LB)
  held || the orders menu open` every frame (frozen in Deployment) - we read THAT flag live from its layer, so the numbers
  show exactly with the markers (the orders menu too), and follow any mod that changes the rule; when it cannot be read,
  the rule is copied (HudFrame's new `ShowIndicatorsKey`). HOW IT LINES UP, nothing patched: `FindLayer("MissionFormationMarker")`
  → `GetMovieIdentifier("FormationMarker").DataSource` IS the public `MissionFormationMarkerVM` - its targets carry the
  formation itself and the point vanilla projected THIS frame (median + 3 m); the marker widgets (`FormationMarkerListPanel`,
  a real, non-generated movie) give their live size, alpha and pinned offsets, paired by the exact point (vanilla re-sorts
  its targets far-first every frame, so labels are KEYED by team + formation, grow-only). The label goes to the marker's
  bottom centre exactly as vanilla centres the marker, the same frame (our view ticks after vanilla's; its LateUpdate
  re-lays out before drawing) - any resolution, UI scale, RTS Camera's free camera (it moves the camera vanilla projects
  from; its four marker patches - solo formation hidden, alpha 0.2 near in free camera, distance text, targeting - are
  followed for free, decompiled into `..\reference\RTSCamera-decompiled\`). Hidden with the marker: behind the camera,
  faded (< 5 m), formation gone; a pinned (targeted) marker keeps its label. FALLBACKS, each logged with its reason and
  counted, per show: a marker without its widget / no widgets → the game's points with a nominal size; no layer / data →
  our own projection of the same point (`ProjectedFormationMarkers`). The READ API now keeps formation averages for EVERY
  team (slots by `Team.TeamIndex`, one allocation-free pass every FormationStatsRefreshSeconds; the player still left out of
  his own). 6 settings (78, a new group "Formation markers (hold ALT)"): `ShowAltMarkerStats`, `AltMarkersShowEnemy`,
  Advanced `AltMarkerTextSize` 16 (vanilla's marker brush `NameMarker.Distance.Text`), `AltMarkerOffset` 2,
  `AltMarkerBarWidth` 60, `AltMarkerBarHeight` 3 (0 = no bar) - all live; `FormationBarsAlways` / `FormationBarHeight` stay
  planned (an always-on form). Gates: ModEnabled first, AthleticsEnabled, the switch, Hide battle UI, photo mode, a fight;
  NOT the player (vanilla shows markers after you fall). Fail safe: any error disables the view for the battle ([error] +
  DISABLED line). Logs: `[hud] attached: ALT markers …`, the first show per battle in full (`first shown at … - technique:
  … ; N markers (yours a, allies b, enemy c): <each marker's point, size, distance>; labels (…): <each label's centre x /
  top y>; no label: <why>`), the first values, every fallback, `[summary] hud: ALT markers - shown Nx, on screen N s;
  technique …; formations labelled: yours / allies / enemy …; fallbacks …; errors N`. Tests 401 (+14 `AltMarkerTests`);
  smoke 55 steps (+3: the prefab, the view with stand-in marker sources through 17 scenes, the fail safe; + the master
  switch step); build 0 warnings; deploy.ps1: build, AssemblyGuard, smoke green and INSTALLED (the game was closed).
  Docs: DESIGN §3 item 3 as built + table + planned rows + interpretation 21 + ten groups, PLAYTEST E4 + L7 + counts,
  README, Steam description, CLAUDE summary + layout, AI_NOTES "Step 20" (research, built, UNVERIFIED 1-6). Commits
  696216e, 8de673b, e90d3a2, 862a48a + this.
  (2026.09.28 11.36.17)
- [x] **Step 20b — Anton's tuned defaults: run floor 0.6, the swing animation a straight line to 85%, the pause unchanged**
  (Anton after his playtest, 2026-09-28: "speed (run) floor sweetspot is 60%"; "swing speed does get reduced but to 85% …
  the delay in seconds is nice, leave it be"). defaults.json: `MinMoveSpeedMultiplier` 0.7 → 0.6, `AttackAnimationMinPercent`
  100 → 85 (DESIGN's Default column keeps the initial 0.7 / 100 - tunings; the tests run on them). THE RULE changed: the
  animations play at A + (1 − A) × f (Core `AttackTimerMath.AnimationMultiplier(f, A)`), full speed at the peak line, 0.925
  halfway, 0.85 empty - steps 13-20's max(m, A) would sit at 85% from f 0.81 down. f is read off the APPLIED m
  (`PeakShareOfAttack`, `AnimationForAttack`) because the decorator only knows m: every recompute (ours, the game's) gives the
  same value, no new state, the line moves in m's 0.05 steps; attacks never slowed (ExhaustedAttackSpeedPercent 100) = full
  speed too. Range 5-100 (the line at 0 would freeze a swing at empty); new label + hint. **The finding**: D is MEASURED from
  the real phases, which the animation multiplier slows, so a ×0.85 swing would have made the pause 18% longer - fixed by
  building D at FULL animation speed (played × the multiplier each phase played at - `TrackedAgent.PhaseAnimation`,
  `AtFullSpeed`): the pause D × (1/m − 1), yours and the AI's, is exactly step 13's, the recovery bar unchanged, the slower
  swing only adds its own ~0.14 s at empty. Stats: the verdict's target = fresh ÷ m + the band's played attack × (1 − its
  animation) (`AnimationExtra`, "incl. +0.15 s of slower swing") so the heavier swing never reads "too slow"; the timer's
  floor = played + pause; the timer row and the first-timer lines name the played attack. Hot swap and the master switch
  unchanged (checked). **Config format 4**: a format-3 file's 0.7 and a format 2-3 file's 100 move to the shipped defaults
  once, logged; `Migrate(read, defaultOf)` is the seam the tests use (the schema runs on DESIGN's values). **Anton's own
  config.json holds 0.5 / 90 (his playtest values), so it is kept** - he needs MCM → "Revert all to defaults" (or the two by
  hand) to play 0.6 / 85; his 90 now means the line (×0.90 at empty). Tests 401 → 405; smoke 57 → 58 steps (the decorator on
  the line, a slower swing through the real logic keeps the full-speed pause); build 0 warnings; deploy.ps1: build,
  AssemblyGuard, smoke green and INSTALLED. Docs: DESIGN §2 + table text + interpretation 22, PLAYTEST A1 / C1 / C2 / C6 /
  L1 / L4 / L5, README, Steam description, CLAUDE summary + layout, AI_NOTES "Step 20b". Commits 22c827c, 0ddeb23 + this.
  (2026.09.28 11.59.41)
- [x] **Step 21 — battle pace: AI shield infantry swing ~30% less, other foot melee 15%, AI archers ~30% slower** (Anton,
  2026-09-28: "make the infantry more defensive, especially the guys with the shields, so that maybe they swing 30% less
  (and make that adjustable)" + "make the archers a bit slower, maybe add a delay in seconds that makes them fire about 30%
  slower overall"). Through the EXISTING AI timer (guard up, by input): each AI attack's pause = step 13's tired part
  D × (1/m − 1) + a share by the attack's CLASS, read at its RELEASE from what he really holds (`IWeaponFacts` /
  `GameWeaponFacts`: the off hand's usage IsShield, the main hand's WeaponClass - a pointer read + managed items, safe in a
  hit callback) - never his formation; on horseback = a rider. THE DATA CHECK (Anton's log, the input-era battles): a fresh
  AI melee cycle of 1.80 s on a 0.78 s attack (his own gap ~1.0 s), and a pause ADDS to that gap (1.38-1.51 s after a tired
  pause) - so the brief's m × 0.7 on D alone would have bought 16%, not 30%. RESIZED: the share stretches his whole expected
  cycle, pause = (T + q (D + G)) / (1 − q), G = `AiMeleeGapSeconds` 1.0 (a setting, measured; the summary checks it) → at every
  tiredness q fewer swings than tiredness alone (a fresh shield man 0.76 s, guard up). Archers: flat seconds on top, sized
  from the fresh shot cycles (the log mixes bows and crossbows: bows ~4.5 s → 2.0 s, crossbows ~6 s → 2.5 s: extra ≈ cycle ×
  (1/0.7 − 1)). Riders' melee, thrown weapons and slings: tiredness only; the player: never (no read, no count). FRESH men are
  held now (m 1 + a share); it rides on AttackRatePaceHold + the master switch (off = every pause lifted at once), hot swap
  at the next attack, every lift path and the step-back survival shared. Settings 78 → 83 in a new MCM group "Battle pace
  (AI)" (after the step back; Advanced last): ShieldInfantrySwingsLessPercent 30 (0-90), FootMeleeSwingsLessPercent 15,
  AiMeleeGapSeconds 1.0, ExtraPauseAfterBowShotSeconds 2.0, ExtraPauseAfterCrossbowShotSeconds 2.5 - DESIGN rows,
  defaults.json refreshed, no migration. Logs: `[rate] battle pace (step 21) at mission start: …`, a mid-battle change,
  the first attack of each class with its pause's parts, the first timer's parts; `[summary]` 7 "battle pace" lines per
  class (attacks by N men, pauses by reason, the cycle = attacks a minute per man while fighting, the AI's own gap after a
  pause, the model's cycle at 0 vs with the setting for foot melee, the archers' extra share as an upper bound), the timer
  rows' share, the peak band in the holds. Performance: nearly every foot soldier gets step 16's component (once); the
  smoke holds 1000 men at once - the AI timer's tick ~0.005 ms, 0 bytes allocated over 500 ticks; no cap needed. Tests 405 →
  418 (BattlePaceTests); smoke 58 → 61 steps (the stand-in weapon read for every logic - a native read on a fake agent would
  crash -, the older steps pinned at 0, the battle pace step, the cost step; the master-switch step lifts a fresh man's share
  too); build 0 warnings; deploy.ps1: build, AssemblyGuard, smoke green and INSTALLED. Docs: DESIGN §2 "Battle pace" +
  interpretation 23, PLAYTEST D5 / D6 / L6c, README, Steam description (5370 bytes), CLAUDE summary + layout, AI_NOTES
  "Step 21" (the data, the formula, decisions, UNVERIFIED). Commits 0020499, a21a507 + this. (2026.09.28 12.33.35)
- [x] 22. Anton's tuning + defending costs (2026-09-29, "to make battles slower"): defaults.json RegenRateNearFullPercent
  50 → 100 (the refill a straight line again: empty → full in 60 s at a walk, half in 30, the peak line in 45 - step 14's flat
  branch, checked exact) and MinMoveSpeedMultiplier 0.6 → 0.3 (DESIGN's Default column keeps the initial 50 / 0.7 - tunings, as
  in 20b). NEW: a blocked melee blow costs the DEFENDER - CostPerShieldBlock 1 (shield, right side), CostPerWrongSideShieldBlock 5
  (the engine's CorrectSideShieldBlock false), CostPerWeaponParry 2 (Blocked / Parried / ChamberBlocked without the shield flag)
  × the blow's hero / party-leader multipliers (0.75 / 3.75 / 1.5 a hero, 0.56 / 2.81 / 1.13 a hero leader); Athletics group,
  0-20, 0 = free; 86 settings. Why the defender and how: OnMeleeHit fires once per melee collision with the victim = the man
  whose guard stopped it (decompiled Mission.MeleeHitCallback); a last try block there (after the attacker's part, so his
  release counter is current) → Core BlockMath.KindOf (missiles, kicks / bashes, horse charges, the shield on the back = free)
  → BlockTaken → the defender's BlockTracker (the same attacker's same swing within 1 s = the same blow; a couched lance / an
  untracked attacker by time) → AthleticsMath.ChargeBlock (step 18's kick pattern: Fighter.BlocksPaid, never Blows) →
  AfterCharge. A PAID block restarts the refill delay (effort, as a kick - a man kept busy blocking does not rest); cost 0 =
  nothing; master switch / AthleticsEnabled off = nothing; never an attack pause, a step back or a damage roll; the attacker's
  own blow unchanged; missiles into a shield free (counted). Config format 5: a format 3-4 file's refill 50 and a format-4
  file's run floor 0.6 → the new defaults once, logged - Anton's real config.json (format 4, 0.6, 50) dry-run through the built
  DLL: exactly those two notes. Logs: `[athletics] YOU: shield block (right side) / (WRONG side) / weapon parry …` the first of
  each kind per battle in full, the rest verbose (bucket athletics-block) with the AI's; the settings sentence names the
  prices; `[summary] Athletics blocks paid by the defender N (P points; by riders R): by kind (points) | by you / AI heroes /
  other AI | free, while off, the same blow seen again, missiles stopped by a shield`; the blows line adds the blocks and
  their points. Tests 418 → 427 (BlockTests + the pinned strings / format numbers); smoke 61 → 62 steps (the classifier on
  the game's own AttackCollisionData, the real logic end to end, the master switch); build 0 warnings; deploy.ps1 green and
  INSTALLED. Docs: DESIGN §2 "Defending costs too" + the run / refill notes + 3 rows + interpretation 24, PLAYTEST A1 (the
  migration) / C1 (30% run, the straight refill) / new C1d (blocks) / L4, README, Steam description (5467 bytes), CLAUDE
  summary + layout, AI_NOTES "Step 22". Commit a821f90 + this. (2026.09.29 06.48.00)
- [x] 23. Brace by orders (Anton, 2026-09-29: "make soldiers not swing but only defend when they have reached a certain floor,
  that depends on their current orders, until they have replenished the floor +20%" + "when defending they whip out their
  shields"; mid-step: "+- 5% ... per soldier, once rolled on a battle"). Each AI man (heroes, riders; never you) is looked at
  about every 0.25 s, staggered (TickBrace, a slice of the dense list): his bar = points ÷ his FULL pool vs his formation's
  movement-order floor - Formation.GetReadonlyMovementOrderReference().OrderEnum: charge / charge a target / attack a gate 20
  (BraceFloorChargePercent), advance 40 (BraceFloorAdvancePercent), everything else 60 (BraceFloorHoldPercent). WHY the AI
  Move mapping: the team AI advances on MOVE orders (BehaviorAdvance / CautiousAdvance / Vanguard) - read literally every AI
  advance would sit on 60 - so an AI-controlled formation on Move reads FormationAI.ActiveBehavior (advance → 40, tactical
  charge → 20). At or below the floor he BRACES: a fourth wish on step 16's input component (melee attack bits out, a guard
  when he wants to attack, a ready cancelled; a ranged weapon in hand or a ranged action let through) until floor + his
  margin = BraceRecoverPercent 20 ± his own roll of BraceRecoverSpreadPercent 5 (rolled once per man and battle, rescaled live,
  ≥ 1 point), the target capped by his wounds with the band sliding down under the cap (never stuck, never bracing full to
  his cap). The order changing mid-brace applies at his next look. Its own wish: AiInputState.Active = ANY wish, so no hold's
  end lifts another. The shield: BraceWieldShield - TryToWieldWeaponInSlot (a one-hander first after a two-hander), checked
  every 1 s, at most 3 calls a brace, measured (the AI's weapon choice is native - whether it puts the shield away is only
  visible in game); BraceRaiseShield - DefendDown in a frame with no guard of his own (EnforceShieldUsage NOT used: vanilla's
  formation update rewrites it for every AI agent; RBM needs Harmony). Ends: refilled, the order changed, wound cap, switched
  off (master switch / AthleticsEnabled / BraceEnabled - all at once), left the field (no engine call), the player took him,
  the hideout fresh start, mission end. FormationAthleticsStats gets Bracing / Ready / Total for step 24. Logs: [brace]
  mission start / settings change / switch-off lines, the first brace and its end in full, verbose per brace; 7 [summary]
  "brace" lines (who, by order, time share, lengths + still bracing at the end = the "turtle" check, ends, margins, the shield,
  the GUARD while bracing + attacks that still started). Settings 86 → 94 (a new MCM group after Battle pace). Tests 427 →
  442 (BraceTests); smoke 62 → 65 steps (Program.Brace.cs: the real logic with a stand-in IBraceBody end to end; the cost -
  1000 men, 400 bracing, 0.003 ms a tick, 0 bytes; the master-switch step lifts braces); build 0 warnings; deploy.ps1 green
  and INSTALLED. Docs: DESIGN §2 "Brace by orders" + 8 rows + interpretation 25, PLAYTEST D7 + L6d, README, Steam description
  (5853 bytes), CLAUDE summary + layout, AI_NOTES "Step 23" (the UNVERIFIED list: turtling, the shield sticking, the order read).
  Commits 9006dc7, 0b4966d + this. (2026.09.29 07.10.53)

- [x] 24. The ready count per squad (Anton, 2026-09-29: "some indication above the squad of ready men, that are not resting in
  their current order status, somewhere near the athletics info on the ALT and on the formations view"). "ready 34/50" = the
  men NOT bracing (step 23's FormationAthleticsStats.Ready of Total - the player left out, as every strip number) on the
  orders-menu strip (a third row under each cell's bar, left, at OrderStripReadyOffset 24; the fallback panel's rows after the
  health) and under every side's ALT marker label (a third line under the bar, centred). Plain (the text brushes' own
  #E8E8E8) while most are ready, yellow at or below ReadyYellowBelowPercent 75, red at or below ReadyRedBelowPercent 50 (the
  most alarming wins); HIDDEN while bracing cannot happen (master switch first, AthleticsEnabled, BraceEnabled) or with
  ShowReadyCount off - one switch for both places. Why a row under the bar: the 35 px beside the vanilla order icons already
  hold "72% ± 8"; the cell grows to 39 px = the 40 px gap between stacked cards, and StripLayout counts the row only while it
  is drawn, so with bracing off the cell is step 9's 23 px again; at 1080p a column's bottom card (24 px to the edge) gets its
  cell lifted 16 px while the row shows (logged). Values at the views' existing pushes (texts rebuilt only on a changed
  number, cached colours - nothing per frame); live for every switch and threshold. Logs: the values lines carry "ready
  34/40" and the rules' sentence; one new [summary] line per view (shown by colour, the fewest ready and which formation,
  hidden by reason). Core ReadyCount.cs (ReadyRules / ReadyMath / ReadyCountStats), StripLayout's ready row; settings 94 →
  98 (no migration); tests 442 → 460 (ReadyCountTests); smoke 65 → 67 steps (Program.Ready.cs through the real strip and ALT
  views - texts, colours, the deeper cell and the lifted bottom card, the panel, new stats, live switches, the master switch,
  the lines; the older steps pin ShowReadyCount off); build 0 warnings; deploy.ps1 green and INSTALLED. Docs: DESIGN §3 (strip
  + markers) + 4 rows + interpretation 26, PLAYTEST D7 / E4 / L7 / "what to send", README, Steam description (5964 bytes),
  CLAUDE summary + layout, AI_NOTES "Step 24". Commits 68d7151, fd23961 + this. (2026.09.29 07.26.44)

- [x] 25. Overall damage reduction (Anton, 2026-10-10: "25% overall damage reduction on all sources (configurable) ... 100
  dmg on average -> 75 ... further slow down the combat"). New setting DamageReductionPercent (25, 0-90, live; group "Damage",
  renamed from "Damage randomness"). Every hit our damage decorator sees on a person, a horse or a shield is multiplied by
  (1 − r/100) AFTER the ±50% roll - the order per hit: the game's models → the roll → the reduction → the game rounds; a 100
  hit rolls 50-150 and lands 37.5-112.5, 75 on average. Independent of the roll's switches (roll off / spread 0 / a toggle
  off: the game's value is still reduced) - "random" and "weaker" are two things. NOT reduced: falls (not a blow), objects
  (gates, siege engines, ships - sieges keep their pace); a 0 hit stays 0 (blocked blows' vanilla 0 untouched), a positive
  hit never below 1, the top is 90 so nothing is unkillable; master switch off = vanilla, counted last like the roll's
  ModOff. Shields reduced with no toggle (all sources; for Anton to overturn). Core DamageReduction.cs (pure, tested),
  DamageRules.ReductionPercent, DamageStats' reduction counters + the [summary] "damage reduction" line (the game's → after
  the roll → landed, -% from each, skips by reason); DamageRandomizer.Reduce on both paths, the first reduced hit logged in
  full, ", reduced → N" on the verbose lines, the mission-start line states it. No config migration (an absent key takes
  its default). Settings 98 → 99; tests 460 → 482 (DamageReductionTests); smoke 67 → 68 steps (the real decorator: mean 75,
  roll off, never falls / objects / 0, never below 1, live, master switch, lines; the older damage steps pin 0); build 0
  warnings; smoke all passed; deploy's install copy hit a DLL lock (game running) - deploy again with the game closed
  (D: not mounted here: deploy.ps1 -GameFolder / -McmBinFolder at the C: install). Docs: DESIGN §1 section + row +
  interpretation 27, PLAYTEST C8 + L3, README, Steam description (6196 bytes), CLAUDE summary + layout, AI_NOTES "Step 25".
  Commits a9e3be6, 0b9d92b + this. (2026.10.10 20.59.53)
- [x] **Step 26 — launch prep** (Anton, 2026-10-10: "start prepping it for launch - it makes the combat slower, makes
  tactics more important, makes heroes a bit stronger"; model ..\better-skirmisher-separation). Anton's F12 shots found in
  the C: Steam userdata (20:52-20:54 = five battle shots of ours; 20:58+ = another mod's Party Steward, left out), copied to
  /screenshots/originals (git-ignored, the sibling's choice). tools/make_thumbnail.py (the sibling's recipe: crop, paint
  out stray labels, fill the HTML template, headless Edge) → tools/preview_thumbnail.jpg 1024x1024 273 KB: a real battle,
  the hold-ALT close-up (the enemy's "ready 1/9" in red beside your shield wall's "ready 37/53") under a "Hold ALT · who is
  tired?" tab, your two bars, "Trax Combat Enhancements - Slower, tactical battles. Heroes matter."; iterated by eye and
  checked at 256 px; JPEG because a photo PNG is over Steam's 1 MB (the old no-game-art PNG removed). tools/make_gallery.py
  → docs/screenshots/01-05 (2560 px, < 1 MB, in order with captions). STEAM-DESCRIPTION.bbcode rewritten (6009 bytes):
  pitch + "not an overhaul" first, then short sections - what changes, tired men, heroes, see it, make it yours,
  compatibility (RBM named politely as the deep overhaul this is the light road beside), FAQ, removing it, first release,
  credits; every number checked against defaults.json, the MCM labels against SettingsSchema. README in the sibling's
  shape. Upload kit: WorkshopCreate/Update.xml pointed at a C:\Users\Trax folder that is not on this PC - now the real repo
  path + the C: game; the create task's short pitch + .jpg preview; tools/make_workshop_update.py from the sibling (sends
  the description + visibility with an update); WORKSHOP-UPLOAD.md "The images" + the gallery list + the C: package line.
  package.ps1 dry run (C: game): build, unit tests, AssemblyGuard, offline smoke passed; 9 files 833,104 bytes, zip
  328,497 bytes. Version NOT stamped (v0.1.0 or v1.0.0 = Anton). CLAUDE layout, AI_NOTES "Step 26". (2026.10.10 21.12.13)
- [x] **Step 27 — Release v1.0.0** (Anton, 2026-10-10: "v1.0.0, go ahead and upload it, git and steam live"). Stamped
  module/SubModule.xml v1.0.0 and the docs that named v0.1.0 as current (4cd40cd, pushed BEFORE packaging - the DLL
  carries that commit). package.ps1 on the C: game: build, unit tests, AssemblyGuard, offline smoke green; 9 files
  833,067 bytes, zip 328,486 bytes. The official uploader with WorkshopCreate.xml ONCE: "Item created. Item ID is
  3817259431", "Uploading done!" (Private, short pitch, tags, preview). The id into WorkshopUpdate.xml; make_workshop_update.py
  --notes "v1.0.0 - first release." --visibility Public → the update upload: "Uploading done!". Steam's public
  GetPublishedFileDetails confirms: title, visibility 0 (Public), 833,067 bytes, the full 6 KB description. README links
  the Workshop item + the GitHub release; WORKSHOP-UPLOAD.md status / item; CLAUDE "Release" line. Tag v1.0.0 on 4cd40cd,
  GitHub release v1.0.0 with the zip. Left to Anton: the five gallery shots on the item page. (2026.10.10 21.17.03)
- [x] 28. Duel deadlock fix, part 1 - no formation braces at 20%, each man's own floor ±10 (Anton, 2026-10-12, a tournament
  log: the 1v1 fighters have NO formation, so step 23 put them on the hold floor 60% - 11 of 16 AI braced, 62% of their time,
  the longest brace 163 s, the AI blocking ~95% of his swings). WHAT: a fourth floor group BraceOrder.NoFormation (tournaments,
  arenas, duels - Agent.Formation null) on BraceFloorNoFormationPercent 20 (0-100; the hold group keeps stop / move / retreat /
  fall back / follow / no order); every AI man's FLOOR = his order's ± his own roll of BraceFloorSpreadPercent 10 (0-30) - a
  second unit roll (BraceState.FloorUnit) once per man and battle on its OWN dice (BraceFloorDice; the margin's ±5 untouched,
  its seeded smoke sequence unchanged), x the slider read live (rescales everyone at once), clamped 0-100, everywhere. WHY a
  roll on the floor and not only the margin: a line no longer braces as one man, and a duel opponent's floor is not
  predictable. Band(r, order, top, unit, floorUnit): his floor is F, so the hysteresis (target = his floor + his margin, at
  least 1 point), the wound cap's sliding band and "never stuck" are step 23's rules unchanged. Logs: the settings sentence
  (no formation 20% → 40%; "± his own 10 (BraceFloorSpreadPercent …)" when above 0), the first brace ("the no formation floor
  20%; his own floor 23.4% = + 3.4 points by his own roll"), the summary 8 lines - by floor + no formation, a new "brace
  floors" line (offsets min / avg / max, braces by floor-offset band). Settings 99 → 101 (DESIGN rows, schema, defaults.json
  refreshed, MCM via the schema); no config migration (missing keys take their default and are written in; format 5 stays).
  Tests 482 → 487 (BraceTests: the no-formation floor, the floor roll - range, apart from the margin, live, clamped at 0 / 100,
  hysteresis over random rolls, the wound cap - the summary). Smoke 68 → 69 (BraceNoFormationAndFloorRoll through the real
  logic; step 23's checks pin the floor roll at 0, the 1000-man cost runs with it: 0.003 ms a tick, 0 bytes). deploy.ps1
  green, INSTALLED (this PC needs -GameFolder / -McmBinFolder on C:). Docs: DESIGN, AI_NOTES "Step 28", PLAYTEST D8 + L6d,
  CLAUDE.md, README + STEAM-DESCRIPTION.bbcode (6826 bytes; not uploaded - Anton's call). (2026.10.12 10.51.42)
- [x] 29. Duel deadlock fix, part 2 - only a WRONG-side shield block restarts the refill delay (Anton, 2026-10-12, the same
  tournament 1v1: both men sat at empty for good, because since step 22 every PAID block set the delay clock like a blow, so
  a man defending against a swing every ~1.5 s never refilled; his rule: a skilful defender recovers while he stays on the
  defence). New switch OnlyWrongSideBlocksStopRefill (bool, default true, live; Athletics group after CostPerWeaponParry;
  off = step 22 exactly). Core: AthleticsMath.BlockRestartsRefill(r, kind) = the one rule (switch off or a wrong-side
  block); ChargeBlock hands it to Drain(restartDelay) - false = only the points move: LastBlowTime (the delay clock) and
  Regenerating (the running refill run) untouched, so the bar dips by the cost and climbs on and a block inside a running
  delay never lengthens it; the costs, exhaustion, curves and speed re-target unchanged; blows, kicks and bashes always
  restart it. BlowOutcome.RestartedDelay; AthleticsRules' constructor takes the switch last, default false (older tests
  keep step 22), From reads it. Logs: the YOU block line per kind ("the refill keeps running" / "restarts the refill delay
  (a WRONG-side block …)"), the verbose tail " - the refill runs on", the YOU-exhausted line, the settings sentence, and
  the blocks summary line split - restarted by W / kept running under K, by kind. Numbers (shipped flat refill, 60 s):
  P/40 points refill per 1.5 s vs a parry's 2 x m - a parrying soldier climbs with a pool above 80, a hero above 60, a hero
  party leader always; right-side blocks (1 x m) let everyone climb; a 100-pool hero parrying every 1.5 s from empty nets
  +1 a parry (~38 after a minute), a floor soldier parrying still sinks (by design - the rates decide). Settings 101 -> 102
  (DESIGN row + decision 28, schema, defaults.json refreshed, MCM via the schema); no config migration. Tests 487 -> 498
  (BlockRefillTests). Smoke 69 -> 70 (BlocksLetTheRefillRun through the real logic's BlockTaken; the older steps pin the
  switch off in AthleticsDefaults). deploy.ps1 green, INSTALLED. Docs: DESIGN, AI_NOTES "Step 29", PLAYTEST (the block list
  item 4 + D8 step 4 + the lines), CLAUDE.md; README / STEAM-DESCRIPTION untouched (they never said blocks stop the refill). (2026.10.12 11.02.29)

- [x] 30. Tired AI blocks in a random direction (Anton, 2026-10-12, the tournament 1v1 where the exhausted AI blocked ~95%:
  "the more exhausted an AI man is, the more his block direction is random"). RESEARCH FIRST (RESEARCH §M, decompiled with
  ilspycmd 9.1 into %TEMP% - the ..eference copy is not on this PC): the guard direction is four MovementControlFlag bits
  (the player's guard is only those bits; auto-block mirrors the attacker's direction); the AI's pass OnAIInputSet, but
  whether its own decision is in that word is native - and the game ALREADY has the lever per agent,
  AiRandomizedDefendDirectionChance (DrivenProperty 40) = 1 - (AI level)^3 from SetAiRelatedProperties, recomputed on every
  UpdateAgentStats; a final-round tournament opponent (AI level x2) sits at -1.10 = never random, a recruit near 1. So the
  build uses the stat decorator, not the input bits: w = TiredBlockRandomPercent/100 x (1 - f), the chance written = own +
  (1 - own) x w (own clamped 0-1; w 0 = the game's value untouched) - exactly Anton's mix with the AI's own randomness kept
  underneath. w rides on RetargetSpeed with the speed multipliers (0.05 steps, same recompute budget; RetargetSpeed now an
  instance method); never the main agent; riders too; ModEnabled first, then AthleticsEnabled, then the setting; the forced
  DefendDown guards of steps 16 / 23 stay ours. Nothing runs per frame, no hook, so no flicker and no per-attack state; the
  input-bit replacement (one roll per attack, a timeout) is designed in AI_NOTES as the fallback if the playtest shows the
  engine ignoring the property. Logs: [guard] (new tag) mission start / settings change / the first raised chance in full /
  verbose guard-chance; summary: "tired block direction" (men, recomputes, own -> written by f band, how many over a game
  value below 0) and "AI guard by f" (blocked % - shield right / WRONG side / parry - vs landed, by f band; counted in
  OnMeleeHit before the block's charge). Settings 102 -> 103 (DESIGN row + behaviour + decision 29 - deliberately reverses
  "tired men must not block less" for the AI; schema in Tired fighters after DamageBonusFollowsAthletics; defaults.json
  refreshed). Tests 498 -> 514 (TiredBlockTests). Smoke 70 -> 72 (TiredBlockDirection through the real decorator + logic,
  TiredBlockCostStaysFlat: 1000 tired men block + recompute ~0.1 us a blow, 0 bytes; the older steps pin the setting 0 in
  AthleticsDefaults). deploy.ps1 green, INSTALLED. Docs: AI_NOTES "Step 30", PLAYTEST D8 step 5 + L6e, CLAUDE.md, README and
  the Workshop text one clause each (6916 bytes). (2026.10.12 11.20.12)
- [x] 31. Player-facing docs (Anton 2026-10-12: "waaay too long"; the idea first - slower combat where timing, rest and
  defence/attack matter, fresh shock troops into tired lines - then a glance at how, then tables for the curious). NEW
  docs/HOW-IT-WORKS.md, the player guide in 14 sections, every number computed from defaults.json through Core's rules
  (a scratch script, not in the repo): pool and attacks per bar by skill for soldier / hero / hero party leader; tiredness
  by bar level 100/75/60/50/30/15/0 (pause after a 0.8 s swing - Anton's logs' D avg, not the brief's 0.9 -, animation,
  attack rate, run, best roll, the tired AI's random-pick share); refill times by effort (60 / 80 / 120 s); block costs and
  which stop the refill + step 29's "does a parrying empty man climb" (net points a minute by pool and rank); tired block
  guesses (skilled fighter: w and 0.75 w wrong for a parry, shields forgiving - a wrong-side block; recruits already guess
  in vanilla); brace floors / targets per order with the +-10 / 20+-5 rolls and a worked example (64% -> 81%); step back
  by bar; pace (fresh shield man +0.77 s, ~23 swings a minute vs 33); damage (an average 100 hit lands 38-113 / 38-94 /
  38-75, mean 75 / 66 / 56); heroes and the hideout refill; HUD colours in bar % and the ready colours; tactics; setting
  names only in small print. tools/STEAM-DESCRIPTION.bbcode rewritten short (6916 -> 3042 bytes): hook, 5 idea bullets,
  "How it works, at a glance", the guide's absolute GitHub link, "Good to know" (not an overhaul, MCM optional, v1.4.8,
  RBM named politely), issues + log, credits, licence; the FAQ / removal / long sections went (the guide covers them).
  README.md the same shape (screenshot 01, Workshop + release links kept, the guide linked, a developers' fold with DESIGN /
  RESEARCH / BATTLE_PACING / PLAYTEST / REVIEW). docs/BATTLE_PACING.md kept as developer research, a note at its top points
  players to the guide. CLAUDE.md layout (the guide, BATTLE_PACING, the byte count), WORKSHOP-UPLOAD.md's size note. No
  code; tests 514 green. Nothing uploaded - the Workshop page changes only when Anton pastes it or the next update sends it.
  (2026.10.12 11.25.32)
- [x] 32. Swing stack (Anton, 2026-10-12: a tired 1v1 swing every ~3 s was blocked ~95% and kicks dodged the pause - "chain
  swings stack"). Every fighter STORES swings: cap = max(SwingStackMinimum 2, round(current points / SwingStackPointsPerSwing
  20)) half away from zero (150 full 8, 75 pts 4, empty 2; 50 full 3), spawn full, filled at every fresh start (switch,
  hideout boss), clamped after every charge. An attack spends one AT ITS RELEASE (melee, loose, throw) and is held only at
  0 - exactly until the next comes back; one comes back per the pause that attack would have left (D x (1/m - 1) at his
  CURRENT m, D = his last measured attack, 0.8 s before any; + the AI's battle-pace share) of time NOT attacking (the clock
  stops in a ready / release / the reload after a loose); above the peak line it refills at once. WHY the AI gets a grace:
  step 21 measured the AI's own ~1 s gap after each attack, on top of any pause - without excluding it after a melee swing
  from the stack, a fresh shield man (share 0.76 s < 1 s gap) would never be held and a tired one swing ~37% more (battle
  pace undone); with it the AI's cycle at 0 is step 13's D + P + G. Kicks / bashes (KicksUseSwings) spend one, never start
  a pause; at 0 your kick key is taken out by step 13's gate (researched: MissionMainAgentController sets
  EventControlFlags |= Kick 0x8000 on the key press - cleared in the same pre-tick, on foot only), a held AI man's kick out
  of his own input (step 16's hook gets the event flags; while held BY INPUT; the AI's kick there is unverified), a bash is
  the attack bits already. You: no hold while a swing is stored (the burst chains), the hold from the release that spends
  the last, the countdown retargeted every gate frame to the next swing (hold-to-attack fires the frame it is back). The
  recovery bar: "Swings 4/8" (@LabelText, rebuilt only on change), the fill toward the next, seconds only at 0. Core
  SwingStack.cs (rules, math, the per-fighter stack, stats + 5 summary lines), PlayerAttackTimer pauseOverride /
  StartCountdown / Retarget, AttackRecoveryReading.WithStack, PaceNotHeld.FromStack; Module AthleticsLogic.SwingStack.cs +
  hooks in Engine / AttackRate / PlayerTimer / Hideout / AiInputHook (HoldKicks) / PaceBody (StackWait) / TrackedAgent
  (Swings, StackD); ResetPhases now an instance method (counts the burst a fresh start cuts). Settings 103 -> 107
  (DESIGN rows + decision 30, defaults.json via DefaultsTool, MCM). Tests 514 -> 531 (SwingStackTests). Smoke 72 -> 75
  (the real logic for you and the AI by input, the bar through the real view, 1000 men waiting: 0.087 ms a tick, the stack
  pass 0.035 ms, 0 bytes); the older steps pin the stack off (SwingStackOff in AthleticsDefaults). Docs: DESIGN §2 + §3,
  AI_NOTES "Step 32", PLAYTEST D9 + L6f, HOW-IT-WORKS section 4 "The swing stack" (stored swings by pool x bar, refill per
  swing and per full stack by bar; sections renumbered), README + Steam description one clause each (3117 bytes), CLAUDE.md.
  Deployed (AssemblyGuard + smoke green). Attack-rate verdicts: same math, read over a whole fight - bursts read fast, the
  waits slow. (2026.10.12 12.52.48)
- [x] 33. Sprint (Anton, 2026-10-12: "a short burst of speed, meant to feel a little like a dodge, and for the AI to make
  charges sharper"). RESEARCH FIRST (§N, AI_NOTES "Step 33"): MaxSpeedMultiplier is the base models' whole speed factor
  (0.63-0.91, perks multiply it), no managed clamp, the engine's top follows it linearly in Anton's own logs - so a sprint is
  the run lever the tiredness already uses, no patch (UNVERIFIED above ~1.0: a native cap, the feet); Left Ctrl is bound only
  to the campaign panels' stack modifier (Shift = zoom, Z crouch, CapsLock walk, Alt markers) - default LeftControl; a
  movement order change is POLLED per formation every 0.25 s (the event fires on every SetMovementOrder); the game caps a
  formation man's speed against the formation's average top (cached ~2 s), so whole formations sprint together. BUILT: Core
  Sprint.cs (SprintRules / SprintMath / SprintStore / SprintReading / SprintStats - store = points / 20 s, clamped on a
  drop, refill 7 s x0.5 moving x the tiredness line to 0.5, 20% to start, 1.6 for the first 30% of T then a line to 1.1,
  0.05 steps, drain only moving); a new setting type ParamType.Key + KeyNames (SprintKey is a key NAME in the file /
  defaults.json, an MCM text box, an unknown name refused); AthleticsLogic.Sprint.cs (your key - a press starts, letting go
  ends, refusals; the AI on a NEW order kind - charge 100%, advance / move 50%, hold none; melee 3 m, a new order, a horse,
  control, the switches, left the field, the hideout fresh start, mission end; no Athletics refill while sprinting; the
  speed check engine vs asked) + SprintBody.cs (ISprintBody / GameSprintBody; step 23's order read shared as
  GameBraceBody.KindOf); SpeedFactorsFor = run x sprint (MaxSpeedMultiplier above 1 through the existing decorator); the
  recompute budget's start rotates so a charge's hundreds queue fairly; the sprint bar (SprintBarView / VM /
  TraxSprintBar.xml: "Sprint 6.2 s", the 20% mark, grey / blue / yellow) above the recovery bar. Settings 107 -> 122 (a
  "Sprint" group + ShowSprintBar + SprintBarOffsetAbove), tests 531 -> 550, smoke 75 -> 80 (1000 men on a charge: 0.006 ms a
  tick, 0 bytes, ~12 recomputes a man; older steps pin the sprint off). Docs: DESIGN §2 / §3 1c / decision 31 / 15 rows,
  RESEARCH §N + 2 UNVERIFIED rows, PLAYTEST D10 + L6g, the player guide section 13 (length by points, the profile, the
  refill times), README + Steam one clause each (3239 bytes), CLAUDE.md. Deployed (AssemblyGuard + smoke green). (2026.10.12 13.29.12)
- [x] 34. The AI guard log split by a SHIELD in hand (Anton, 2026-10-12: his tournament log showed tired shield men's random
  direction turning into WRONG-side blocks - 63% at empty, landed 6% - while whether tired SHIELDLESS men parry worse was
  hidden in the mixed line). Pure logging, no gameplay change, no setting. Core TiredBlockStats: one Guard table three times
  (all / with a shield / without), AddGuard(bin, kind, shield), AddRaised(..., shield) + RecomputesWithShield; summary 2 -> 4
  lines: the raised chance ("N recomputes - X with a shield in hand, Y without"), the old "AI guard by f" line unchanged,
  "AI guard by f WITH a shield in hand (when the blow came - step 34)" (hint: the shield still stops a wrong-side block)
  and "... WITHOUT a shield ..." (hint: a wrong direction = the blow lands, empty should land far more than peak). Module:
  ShieldInHand = step 21's IWeaponFacts off-hand read, in GuardTaken after its early returns (OnMeleeHit, before the
  block's charge) and at each raised recompute; a failing read = one [error], counted without. No per-hit line, nothing
  allocated. Tests 550 -> 551, smoke stays 80 (TiredBlockDirection extended: shield per agent, one dropped mid-fight, no
  read for you / Athletics off, the lines verbatim). Docs: AI_NOTES "Step 34", PLAYTEST D8 step 5 + L6e (read the
  WITHOUT line first), CLAUDE.md. Deployed (AssemblyGuard + smoke green). (2026.10.12 13.34.50)
- [x] 35. Sprint tuning (Anton, 2026-10-12, after two tournaments: "the sprint felt very slow" - his log showed the engine
  DOES reach the asked top (asked x1.66 = engine x1.66), but the game's foot acceleration TopSpeedReachDuration (DrivenProperty
  86, per agent; Sandbox 2.5 + max(5 - (1 + skill x 0.01), 1) / 3.5 - min(weight / (weight + gear), 0.8) = 2.3-2.8 s) is
  longer than the full-speed part and his taps were 0.4-0.8 s; tired, the store collapsed (points / 20) and 2 x a tired run
  is still slow). (1) The PEAK on the FRESH run: Core SprintMath.RunFactor = SprintMaxMultiplier x the untired run for the
  first 30%, then a line to SprintMinMultiplier x the CURRENT run - written on MaxSpeedMultiplier IN PLACE OF the tiredness
  multiplier (TrackedAgent.SprintRun, 0 = none, replaces SprintMultiplier); fresh 2.0 -> 1.1, exhausted 2.0 -> 0.33;
  Quantize floors at 0.05. (2) defaults.json SprintMaxMultiplier 2.0 (DESIGN's initial stays 1.6; range 1-3 kept; no
  migration - Anton's config has 2.0). (3) SprintMinSeconds 2 (0-5): capacity = max(floor, points / pps), OnFloor; the refill
  rule unchanged (the whole store in 7 s / the factors - the floor 14 s standing at empty); with a floor no empty-bar refusal
  (a used-up store is below the 20%). (4) SprintTopSpeedReachSeconds 1 (0-5, 0 = the game's): the stat decorator caps
  TopSpeedReachDuration = min(game, ours) only while sprinting - the base model rewrites the game's value every recompute,
  and the start, each 0.05 step, the end and a sprint setting change recompute him; GameReach remembered for the logs.
  (5) Logs: the mission-start sentence, the first-sprint lines (floor, fresh-run peak, run property before -> after, fade
  target, game -> our acceleration), the summary's off-the-floor counts (you / AI), the acceleration clause, the speed check
  x the fresh run. Tests 551 -> 557; smoke 80 -> 81 (SprintTuningThroughTheLogic: an exhausted you through the real logic and
  decorator - 0.8 x 2.0 at the start, TopSpeedReachDuration 2.6 -> 1.0 -> 2.6; step 33's checks pin floor / reach 0).
  Docs: DESIGN (prose, 2 rows, decision 32), AI_NOTES "Step 35", HOW-IT-WORKS section 13 tables, PLAYTEST D10 / L6g,
  CLAUDE.md. Deployed (AssemblyGuard + smoke green). (2026.10.12 15.04.10)
- [x] 36. AI sprint by order (Anton 2026-10-12: "I will position my shock troops behind enemies and they won't use up all
  their sprint before even getting the real charge command"). Step 33 sprinted every new order up to a share of what was
  STORED (charge 100, advance / move 50), so a few moves round the flank emptied the store. Now each order kind has a USE
  and a KEEP, both % of his FULL store (capacity now): he sprints min(use x cap, stored - keep x cap), nothing below 0.2 s
  (plumbing, SprintMath.AiMinSeconds), and the AI has NO SprintMinStartPercent gate any more (the keep is its gate; a
  charge from 10% still sprints 0.5 s; your 20% gate unchanged). Charge (charge, charge a target, attack a gate, the AI's
  tactical charge) 100 / 0; advance (F1 F4 "engage", the AI's advance) 50 / 0; move (Move: F1 F1, or an AI general's Move
  under any behaviour but advance / charge) 20 / 70 - a 5 s store: 1 s from full (80% left), 0.5 s from 80%, none from
  60%; hold / retreat / fall back / follow / follow-entity none. Core: SprintOrder Charge / Advance / Move / None
  (AdvanceOrMove split), SprintStart.ReserveKept, SprintRules' six percents + AiOrdersText, SprintMath AiUse / AiKeep /
  AiAmount / AiReserveStops, SprintStore.TryStartAi, SprintStats reserve kept per kind + the summary by kind with its use
  / keep. Module: OrderChangedFor → TryStartAi, the first-order / first-AI-sprint lines (stored %, use / keep, asked T,
  left after it), one "first AI sprint held back by a reserve" line. Settings (live, group Sprint): SprintAiChargeUse /
  KeepPercent 100 / 0, SprintAiAdvanceUse / KeepPercent 50 / 0, SprintAiMoveUse / KeepPercent 20 / 70 replace
  SprintAiChargeSharePercent / SprintAiMoveSharePercent (DESIGN 2 rows → 6, decision 33; defaults.json refreshed, 128
  settings). Old keys: config FORMAT 6 - Migrate renames SprintAiChargeSharePercent → SprintAiChargeUsePercent (its value,
  unless the new key is set) and drops SprintAiMoveSharePercent, in a file of any format, both out of Unknown so the
  rewrite does not carry them under "Not recognised" (Anton's file has both). Tests 557 → 573; smoke 81 → 82
  (SprintByOrderThroughTheLogic: shock troops move 1 s, 0.5 s, then reserve kept, the charge sprints the 3.5 s left, an
  advance 2.5 s, live keep, master switch, summary; step 33 / 35 smoke lines updated). Docs: DESIGN, AI_NOTES "Step 36",
  HOW-IT-WORKS §13 (order → use / keep table, the shock-troops tip), PLAYTEST D10 step 7 + lines, CLAUDE.md. Deploy:
  build, AssemblyGuard and smoke green; the install copy failed - the game holds the DLL. (2026.10.12 15.17.57)
- [x] 37. The AI sprint: ONE reserve per order (Anton, 2026-10-12: "simpler - one number per order"; AI_NOTES "Step 37").
  An AI man on a new order sprints everything he holds above the order's reserve - stored − keep x his FULL store, nothing
  below 0.2 s - instead of step 36's min(use, stored − keep). Keeps: charge 0 (all of it), advance 50 (100% → 50%, 90% →
  40%, 60% → 10%, 45% none), move 80 (100% → 20%, 90% → 10% - Anton's example -, 60% none): men moved or advanced into
  position keep sprint for the real charge. Core: SprintRules three keeps (the three uses gone), SprintMath.AiSprints
  replaces AiUse, AiAmount = stored − keep x cap; texts "charge keeps 0%, advance keeps 50%, move keeps 80%", the summary
  "move (keeps 80%) N men". Settings: SprintAiChargeUsePercent / SprintAiAdvanceUsePercent / SprintAiMoveUsePercent
  removed (DESIGN rows, schema, defaults.json - 125 settings); SprintAiAdvanceKeepPercent 0 → 50 and SprintAiMoveKeepPercent
  70 → 80 (DESIGN initial = these, new behaviour); MCM hints "keeps N% of his sprint store for later orders". Config FORMAT 7:
  a format-6 file's advance 0 → 50 / move 70 → 80 only at step 36's defaults (logged), any user value kept; the three use
  keys and step 33's two share keys dropped from any format, one [config] note each, out of Unknown. Tests 573 → 575; smoke
  82 steps green (shock troops: move 1 s to 80%, second move reserve kept, the charge sprints the 80%, an advance 2.5 s, the
  reserve live, master switch, summary). Docs: DESIGN (§2 sprint, 3 rows, decision 33 (f)), HOW-IT-WORKS §13 (order →
  keeps, from 100 / 90 / 60%), PLAYTEST D10 step 7 + lines + L6g, CLAUDE.md. Deploy: build, AssemblyGuard, smoke green;
  installed as TraxCombatEnhancements.Dev. (2026.10.12 15.31.22)
- [x] 38. Release v1.1.0 (Anton's yes 2026-10-12: "waaay ahead of the old release which felt really bad" - he playtests details after). Version stamped once in module/SubModule.xml (88ced29); tools\package.ps1 with the C: paths: build, tests, AssemblyGuard, smoke green, dist 10 files 975,074 bytes (new: GUI\Prefabs\TraxSprintBar.xml), zip 378,202 bytes. Workshop item 3817259431 updated through make_workshop_update.py (change notes = steps 28-37 for players + STEAM-DESCRIPTION.bbcode, visibility untouched): "Uploading done!" (the ReadKey crash after it is the known harmless ending). GitHub release v1.1.0 targeted at the stamp commit, zip attached, notes = the same list + the player guide link. README / docs were already on main. (2026.10.12 15.35.29)
- [x] 39. Sprint in towns and villages (Anton, 2026-10-14, F12 shot: Athletics + swings bars up in a town, no sprint bar - "maybe it won't when I'm fighting gangs in the streets"). Cause: GameSprintBody.PlayerCanSprint allowed only the fight modes (Battle / Duel / Tournament / Stealth) and SprintBarView had no outside-a-battle rule ("never outside a battle"). Now PlayerCanSprint also allows the walk-about mode (HudFrame.IsWalkMode = StartUp - towns, villages, the training field, halls); SprintBarView takes the Athletics bar's outside rule (ShowPlayerBarOutsideBattles: a weapon drawn, or below full - here Athletics below full OR sprinting OR the store refilling - then the grace). Street / alley fights run in whichever of the two modes, so both are covered. The AI's sprint is untouched (formation orders only). DESIGN §3 1c + the SprintKey row, HOW-IT-WORKS §13, PLAYTEST D10 step 3 updated; smoke: in town hidden with nothing drawn and a full store, up with a weapon drawn. Build, 575 tests, AssemblyGuard, smoke green; deployed as TraxCombatEnhancements.Dev (deploy.ps1's defaults still name the old PC's D: drive - ran with -GameFolder / -McmBinFolder on C:). Not verified in game: whether vanilla binds Left Ctrl to anything in the walk-about mode. (2026.10.14 11.15.00)
- [x] 40. Release v1.1.1 (Anton's yes 2026-10-14: "commit it, deploy it to steam"). Version stamped once in module/SubModule.xml (2c42885), ChangeNotes in tools/WorkshopUpdate.xml; tools\package.ps1 with the C: game / MCM folders: build, tests, AssemblyGuard, smoke green, 10 files 975,686 bytes, zip 378,299 bytes. First uploader run crashed in LoadTasks - the GetItem comment held "--notes" / "--visibility" (a double dash is illegal in an XML comment; the last release went through make_workshop_update.py's copy) - nothing uploaded; reworded, second run "Uploading done!" (the press-any-key crash after it is the usual). Quirk added to WORKSHOP-UPLOAD.md. GitHub release v1.1.1 with the zip. Description untouched. (2026.10.14 11.30.00)
- [x] 41. The shield wall holds its slots (Anton, 2026-10-17, a playtest: a fresh shield wall waiting for the enemy - men stepped 2-3 m out of it to swing and walked back, so the wall bulged and broke; "like RBM"; AI_NOTES "Step 41", RESEARCH §O, DESIGN §2 + decision 34). WHY it happened: vanilla's HumanAIComponent.ParallelUpdateFormationMovement hands the engine SetFormationIntegrityData(slot, facing, integrity, keep) with keep true only for a man with a 5th-slot item in a mostly RANGED formation - never for an infantry shield wall, so the native AI walks each man at his target (and the shield wall order is a Move, via TryCancelStopOrder). The TIMING research first (the brief's stop condition): vanilla writes it on each AI man's own private timer (_cachedAndFormationValuesUpdateTimer, 0.45-0.55 s, staggered) from Agent.TickParallel inside the agent job that Mission.OnTick starts AFTER every OnMissionTick, plus at once on every order (OrderController.AfterSetOrder → ForceUpdateCachedAndFormationValues); the next frame's Mission.OnPreTick waits for the job and calls OnPreMissionTick - our write point; the timer's public StartTime moves exactly on vanilla's update frames (proved in the smoke on the real Timer), so each held man is rewritten in the very next pre-tick - vanilla's false lives ≤ 1 frame per ~0.5 s per man: the write CAN win, no Harmony. Built: ShieldWallHoldsSlots (on, live, "Battle pace (AI)"; 126 settings): every AI man on foot, in a formation, not detached, in a shield wall / square / circle (the game's defensive set), under any order but charge / charge a target / tactical charge / attack a gate / retreat, not fleeing, gets vanilla's own call with vanilla's own slot point (blend true) and the flag TRUE; RBM's escape (far = vanilla's 6 x speed AND behind the line → left free to rejoin; far ahead = walked back); never you, riders, a siege's attackers (RBM's lesson), naval battles; siege defenders and scattered formations held. Off (the switch or the master switch) writes vanilla's own value back at once (under a Stop vanilla never writes, so "just stop" would have left ours forever); a man let go by a gate gets the same. One read-only reflection (the timer, once per man; a renamed field → a 0.1 s rewrite fallback, logged); none on ShouldCatchUpWithFormation (speed only). Core ShieldWall.cs / ShieldWallStats.cs; Module WallBody.cs (IWallBody seam) + AthleticsLogic.Wall.cs (TickWall from OnPreMissionTick, the held list, the staggered look, the samples); [wall] lines + 5 [summary] "wall" lines: looks by verdict, writes after vanilla / by the look / fallback, vanilla's updates seen and their interval, releases by reason, and every man of a holding wall sampled every 0.5 s - share > 1 m / > 2 m AHEAD of his slot, mean, farthest, all and in contact - split ON / OFF by the switch at the sample (Anton's A/B in one battle). Tests 575 → 610 (ShieldWallTests); smoke 82 → 86 (the stand-in, the game's timer, the real logic end to end, the cost: 1000 men held 0.016 ms a pre-tick, 0 bytes; the master-switch step lets held men go). Docs: DESIGN (row, §2, decision 34), RESEARCH §O + UNVERIFIED 24, HOW-IT-WORKS §10 table, PLAYTEST D11 + L6h, CLAUDE.md / AGENTS.md. Build, tests, AssemblyGuard, smoke green; the install copy failed - the game was running (DLL locked): close it and run tools\deploy.ps1. Only the game can tell: whether the native AI honours the flag for infantry (wall slots ON vs OFF in contact) and that held men still swing at what reaches them. (2026.10.17 19.12.41)
- [x] 42. Shield wall: a small step out (Anton, 2026-10-17, his playtest of 41: the hold WORKED - in contact 0.6% of men > 1 m ahead of their slot ON vs 17.8% OFF (mean 0.09 vs 0.70 m, farthest 9.6 m off) - but with it on they "looked many times like they were about to reach to hit but stopped, maybe as the enemy took a few steps back, so they very rarely hit"; off they ran far out of the square; his wish: "give them just a bit of a step if the enemy moves a small step back, but not as far"). Built: a LEASH per held man, ShieldWallStepOutMetres L (1.0 m - "just a bit of a step"; 0-3, live, right after ShieldWallHoldsSlots; 127 settings; no config migration - a new key takes its default). Two states with hysteresis (Core LeashMath, ShieldWallLeash.cs): FREE = vanilla's own value given back (so the native AI closes on its enemy and swings) while he stands less than L out of his slot AND an enemy - the native AI's target, else his immediate enemy - is close in front of the SLOT: in front of its line and within L + 2 m of it (StrikeReachMetres 2: arm ~0.7 + blade ~0.9 + the target's body ~0.35 - measured from the slot so a step of at most L brings him into reach; an enemy farther off would only draw him to the leash and back without a blow; + 0.5 m while he is out, so an enemy on the edge does not flip him); HELD (step 41's TRUE) at L or more out, or no enemy close; after a step he may step again only once back within min(0.3 m, L/2) of his slot (BackInSlotMetres: step 41's held men stood 0.09 m out on average). "Out" = the formation's facing for a shield wall, AWAY FROM THE CENTRE for a square / circle (their sides and backs face outwards - along the facing those men could never step). TIMING: a man out is looked at every 0.1 s (LookSeconds - he walks 1-3 m/s, a 0.25 s look could let him 0.75 m past L) and NOT rewritten at vanilla's ~0.5 s update (vanilla writes false anyway - that is the step); the held men keep step 41's cadence (after vanilla's update + the 0.25 s look); the enemy is read (native) only for a man who could step now or is out, never at L 0. L = 0 runs step 41's code path exactly (no read; the smoke proves the same writes with and without an enemy in front, and step 41's own smoke now runs pinned at 0). Every let-go path ends a step (a gate, the switch, the master switch first, leaving the field, the mission's end). The alternative (hand the engine a slot point moved up to L toward the target instead of on/off) was NOT taken: the integrity position goes straight to native code (Agent.SetFormationIntegrityData → MBAPI) and nothing managed shows how the engine uses it - noted as the fallback idea (AI_NOTES "Step 42"). Logs: the leash in the [wall] mission-start sentence, a live L change, the FIRST step out and its end in full, verbose steps (bucket wall-step), "by the step-out leash" writes, and the [summary] "wall step out" line - steps by men, steps a man a minute while held, share of held time out, each step's farthest out (mean / max), how steps ended (reached the leash / enemy gone / leash set to 0 / let go / fell / still out), the overshoot past L of those reined, back in their slots after a step, and in-contact samples past L and past L + 0.5 m. Core ShieldWallLeash.cs (WallLeash, LeashMove, LeashEnd, LeashMath, WallLeashStats), WallRules.StepOutMetres / Leash, WallWriteKind.Leash; Module IWallBody.TryEnemy (GetTargetAgent, else ImmediateEnemy, an active enemy only), WallState.Leash, AthleticsLogic.Wall (the leash in LookWall, the 0.1 s look in the per-frame loop, Manage, NoteLeash, EndStep). Tests 610 → 625 (ShieldWallLeashTests); smoke 86 → 89 (the real logic: reined at 1.03 m walking out at 2 m/s, the return, the next step, enemy gone, L 0 / 2 live, the master switch, the summary; L 0 = step 41 exactly; the cost - 1000 men, 200 out, 0.015 ms a pre-tick, 0 bytes). Docs: DESIGN (row, §2, decision 35), AI_NOTES "Step 42", RESEARCH §O item 8, HOW-IT-WORKS §10, PLAYTEST D12 + L6h, CLAUDE.md / AGENTS.md. Deploy: build, AssemblyGuard, smoke green, installed as ModulesTraxCombatEnhancements.Dev (a first try was blocked by Windows Smart App Control on the freshly built unsigned DLL - a per-hash verdict, not our code; the next commit's build passed - AI_NOTES "Step 42" gotcha). Only the game can tell: that a freed man really closes and lands blows (Anton's eye), that nobody chases far (in contact past L + 0.5 m near 0), and the step rate. (2026.10.17 19.43.43)
- [x] 43. Aggression follows Athletics + the FEVER of a charge + cheaper swings (Anton, 2026-10-17: men are defensive by nature, they won't swing until they tire; when the general says NOW! everybody surges, veterans longer, tired men shorter, and the surge fades; today AI men ran into each other swinging until exhausted - our tiredness was the only brake; tournaments calmer; BIG values first "to see the effect well"). Three changes from Anton mid-step, all built: the fever sprint FADES with the fever (a roll, not once per fever); CostPerWrongSideShieldBlock 5 → 2 in the same migration; and THE REDESIGN - the first draft's flat calm factor (AiCalmFactor 3, a x3 peak) was replaced before any release (no switch for it) by "a man at work doesn't hold a pace until he drops; the more tired he is, the less he swings". Built: (A) every AI man ON FOOT's two melee attack-decision values (the base model's SetAiRelatedProperties assigns them with = every recompute: AIAttackOnDecideChance = 0.1 x 0.48 x (3 − defensiveness) clamped 0.05-1 - charge 0.144, other orders 0.096, a non-charging shield wall / square / circle 0.05 - and AIAttackOnParryChance 0.08 − 0.02 D; neither reads the AI level - RESEARCH §P) x a SHARE sliding in a straight line with his bar (points / FULL pool - the brace's reading, not f; a wound's cap is in it, no special case) from AiAggressionFreshPercent 60 to AiAggressionExhaustedPercent 20 - a charge's 0.144 → fresh 0.086, half 0.058, empty 0.029; written by the stat decorator (SpeedPenalty.ScaleAttackDecisions, x then clamp, no floor of ours - the game's 0.05 is its own formula's) in 0.05 steps that ride RetargetSpeed (every charge, refill step, wound, settings change) under the same 50-a-tick recompute budget; never the player; never riders (a rider's swing comes with his pass - a less eager rider rides through without striking; step 21's reason too); never ranged (archers keep step 21's wait); x step 5e's m when AttackRateAiDecisions is on (multiplied). A tired man now has TWO brakes - fewer decisions and the unchanged pause / battle pace / swing stack / brace floors (Anton playtests the balance first). (B) THE FEVER: a NEW charge / advance order (the sprint's order poll - PollOrdersFor now runs while the AI sprint OR the fever is on; charge, charge to a target, attack a gate, the AI's tactical charge / advance on a Move; your F1+F3 / F1+F4 and the AI general's - SprintFormationRead.ByPlayer) arms each AI man on foot with his CURRENT points / FeverPointsPerSecond 10 s at an ABSOLUTE FeverAggressionPercent 80 (armed: max(tired, 80%); burning: tired + (80% − tired) x left / full, never below tired, landing on his CURRENT tired share; a man above the peak gets nothing); the clock starts at his first enemy within FeverContactMetres 5 m (one GetNearbyEnemyAgentCount per AI man on foot every ~0.25 s, staggered like the brace) and burns x 1/FeverShieldDurationMultiplier 0.7 while a shield is in his hand, read live at every look (step 21's IWeaponFacts); the same order group again leaves it alone, the other aggressive group re-arms to max(left, fresh); a hold / move / retreat / stop / follow ends it, armed or burning (the general called them back); no formation (tournaments, arenas, duels) = no fever, the bar's share only; a man who mounts or is taken by the player loses it; heroes the same rules (their big pools = long fevers). The FEVER SPRINT (FeverSprintToClose): every FeverSprintCheckSeconds 1 s while armed or burning, with sprint above his order's reserve (step 37), not sprinting, an enemy FeverSprintMinMetres 3 - FeverSprintMaxMetres 12 m off, a roll at FeverSprintChancePercent 50 x left / full (full while armed) → step 36's TryStartAi; several per fever, rarer as it fades. (C) defaults.json CostPerBlow 10 → 3 and CostPerWrongSideShieldBlock 5 → 2; config FORMAT 8 moves an older file's 10 / 5 once (both defaults are as old as steps 5c / 22, so any older format, not only 7), tuned values kept, logged; HOW-IT-WORKS' tables recomputed (attacks per bar 17-178, the free quarter, the swing-stack example, the block costs, the tired shield man; the parry-climb table is the parry's, unchanged); DESIGN's Default column (the tests' initial values) untouched. 12 settings (139) in a new group "Aggression and fever (AI)" after "Sprint", all Live; Core Fever.cs (FeverRules, FeverMath, FeverStore + enums) and FeverStats.cs; Module AthleticsLogic.Fever.cs, TrackedAgent's fever fields, the decorator, RetargetSpeed → RetargetDecision. MEASURE ([fever], a new tag): the settings line with a charge's values fresh / half / empty / peak; first values written / armed / burning / fever sprint / faded in full; 10 [summary] lines - armed by order (you / the AI general), re-arms, contact delay mean / max, never met, burn length shield x0.70 vs none, decision recomputes per AI man-minute and per fever, fever sprints by the fever left + refusals by reason, and THE PROOF: AI melee attacks per man-minute in contact by bar band 100-75 / 75-50 / 50-25 / 25-0 % without a fever, in a fever, and on its own line in no formation (tournaments), plus the values written by band and in a fever. Recomputes: the share moves a step per 12.5% of the bar (8 over a full bar) - a few a man-minute in a fight, ~5 per fever (the arming + 4 steps down from 0.8 to 0.6). Tests 625 → 642 (FeverTests); smoke 89 → 91 (the real decorator and logic: 0.144 → 0.086 / 0.115 armed / 0.108 mid-fade / back to 0.086 after 5 recomputes / 0.058 at a half bar, never you or a rider, the shield's 4.9 s for 7 s of store, the proof's groups, the fever sprint and its refusals, re-arming, live settings, the master switch; the cost - 1000 men in a fever, 0.005 ms a tick, 0 bytes); the older smoke steps pin the share at 100 / 100 and the fever off. Docs: DESIGN (rows, §2 "Aggression and the fever", decision 36), RESEARCH §P + UNVERIFIED 25, AI_NOTES "Step 43", HOW-IT-WORKS §11 (new) + recomputed tables, PLAYTEST D13 + L6i, CLAUDE.md / AGENTS.md. Gotchas: sed -i in Git Bash rewrote two CRLF test files as LF (restored; Python with newline='' after); Smart App Control blocked the smoke's fresh DLL once (step 42's per-hash verdict) - a commit and rebuild passed. Only the game can tell: that the native AI follows the two values (the proof's no-fever row falling band by band - RESEARCH UNVERIFIED 25), that a charge visibly surges and fades, and that two brakes slow tired fights without deadlocking them, in the field and in a tournament. Deploy: build, AssemblyGuard, smoke green (91 steps), installed as Modules\TraxCombatEnhancements.Dev (Smart App Control blocked the DefaultsTool's fresh check build once - not ours; the unit tests check defaults.json strictly). (2026.10.17 21.20.51)
- [x] 44. The fever mark under ALT (Anton, 2026-10-17: when I hold ALT the game shows a small icon above each of my soldiers - show me there which ones are in FEVER, step 43). WHY our own mark and not a tint of vanilla's icon (Claude's call, told to Anton): vanilla's per-man icon is MissionAgentLabelView's 3D banner mesh on each agent's visuals, kept in a PRIVATE dictionary, shown for your side only, and its vector arguments (colour, highlight, alpha) are REWRITTEN on every highlight change and every tick near the camera - a tint of ours would fight it (RESEARCH §Q). Built: FeverMarkView (a TraxHudView like step 20's ALT labels) draws a small 2D bar of ours above each AI man whose fever runs (armed or burning) EXACTLY while vanilla shows its formation markers - step 20's own live flag (MissionFormationMarkerVM.IsEnabled: ALT held or the orders menu open; its rule copied when unreadable) - WHY that and not the icon's own rule (ALT / the "always show banners" option / only the SELECTED formations while the orders menu is open): one rule for both ALT overlays, readable (the icon's state is private), and the orders menu is exactly when you decide to charge. FeverEnabled off = no marks (first in the view's condition, its own line); HudGate's rules (ModEnabled FIRST, AthleticsEnabled, ShowFeverMarks, Hide battle UI, photo mode, a fight); the player need not be on the field. THE LOOK: a slim bar 22 x 5 UI px (the game's UI scale applies), a dark 1-px frame, the FILL = the fever left (full armed), BRIGHT ORANGE #FF8C1A armed and at the burn's start through YELLOW #FFE040 as it empties, alpha 1 → 0.45, gone at 0 - WHY a bar: its length says how much surge is left, its colour the state, from the one native sprite every Trax bar uses (Gauntlet cannot rotate a square into a diamond). WHERE: the feet + FeverMarkHeightMetres 2.5 m (just over vanilla's 2 m icon) through MBWindowManager.WorldToScreen from the combat camera every frame (rides the man, follows RTS Camera's free camera), vanilla's own icon fade (0 under 1.5 m, a line to 8 m) baked into both colours, full size to 20 m → 60% at 60 m. WHO: yours and allies' always, the enemy's behind FeverMarksShowEnemy (on - vanilla's icon never marks the enemy; seeing their surge spent is the point); never the player, never a calm man. COST: a new public read AthleticsLogic.TryGetFever(agent, out armed, out share) + its allocation-free walk over the dense array (FeverWalkCount / TryGetFeverAt; 0 while no fever runs); every eligible man projected BEFORE the pick (an off-screen man never takes a slot from one on the screen - the first draft projected only the picked, and in a crowd the nearest men are often off the screen's sides), then the NEAREST FeverMarkMax 200 (Array.Sort keys/items, allocation-free, only when the cap bites) into a grow-only pool of marks (mark i = the i-th drawn this frame); the frame's arrays reused, doubling only past their most. Measured in the smoke: 1000 men, 400 in a fever (60 behind the camera, 78 off the screen, 62 past the cap), 200 marks moving every frame - 0.03 ms a frame, 0 bytes (+ in game the engine reads per man and Gauntlet's layout of the bars). 6 settings (145), all Live: ShowFeverMarks, FeverMarksShowEnemy, FeverMarkMax 200 in the ALT group - renamed "Hold ALT: formation markers and fever marks" - and Advanced FeverMarkWidth 22, FeverMarkHeight 5, FeverMarkHeightMetres 2.5 (step 20's precedent: layout numbers are settings; the colours UI constants like the bars'). Core FeverMarks.cs (FeverMarkSkip in the order asked, FeverMarksHidden with ModOff first, FeverMark, FeverMarkFrame, FeverMarkRules, FeverMarkMath - Tint / Fade / SizeFactor / PreSkip / Pick / Size / Place / OnScreen -, FeverMarkStats); Module Hud/FeverMarkSource.cs (IFeverMarkSource + GameFeverMarks), Hud/FeverMarkView.cs, Hud/FeverMarksVM.cs, module/GUI/Prefabs/TraxFeverMarks.xml, attached after the ALT labels. LOG: [hud] attached / layer created (the first in full, later shows verbose), "first marks drawn at … - N men in a fever (yours and allies, enemy; armed, burning), N drawn (the nearest N of M), skipped by reason, screen and UI scale, the layout, the nearest three, the source", "FeverEnabled switched off", and the [summary] "hud: fever marks" line (shows, seconds over frames, marks a frame mean / max, the most men in fever by side, frames at the cap, men skipped by reason summed over frames, the view's own cost in µs, errors); every entry fail-safe (an error disables the view for the battle). Tests 642 → 655 (FeverMarkTests: colour and alpha by state and share, the fade, the size, placement, the skips in order, the nearest-N pick and the reused / grown frame, the hidden reasons with the master switch first, the settings, the sentence, the summary). Smoke 91 → 95 steps (the prefab through PrefabIsValid; the real view over the REAL logic's fevers - armed / burning 0.7 / a faint yellow sliver / faded, calm, you, behind, too close, off the screen, not placed, the enemy switch, the cap, the fade and the size, the height / length live, UI scale 4/3, vanilla's flag and the copied rule, no camera, FeverEnabled off, a hold, the gates, the lines and the summary; the fail safe; the cost) + the master-switch step. Docs: DESIGN (6 rows, §3 item 3b + "The fever marks as built", decision 37, the group's name), RESEARCH §Q + UNVERIFIED 26, AI_NOTES "Step 44", HOW-IT-WORKS §11 + §15, PLAYTEST D14 + the summary block, CLAUDE.md / AGENTS.md. Gotchas: a smoke step that calls AthleticsDefaults() must end with ConfigStore.Reload (DamageDefaults inside pins DamageRandomPercent; the MCM steps need the file's values back); AthleticsLogic.Failed reports a site once per mission, so the fail safe proves its error by the view's own count; Smart App Control blocked the smoke's fresh Core DLL once (per hash - a one-line doc edit passed). Only the game can tell: that the bar sits over the head just above vanilla's icon and rides a running man without lag, the 1-px frame and the fill at 5 px, the colours' fade, the enemy's marks on their charge, and that 200 marks cost no visible frame time (PLAYTEST D14, RESEARCH UNVERIFIED 26). Deploy: build, AssemblyGuard, smoke green (95 steps), installed as Modules\TraxCombatEnhancements.Dev with GUI\Prefabs\TraxFeverMarks.xml. (2026.10.17 21.46.30)
- [x] 45. Research + plan, no code: the IMPACT system (Anton, 2026-10-17, while playtesting 41-44: kicks / bashes, blocks in the right and the wrong direction and clean hits can stagger, drop a shield or knock down, by both men's CURRENT Athletics - pool AND fill -, the weapon's weight, swing speed and bluntness, the skill; longer, more random staggers and slower get-ups when exhausted; the player too; "put it in order for me, I may be missing something" - 12 points incl. an unfinished one about the damage dealt). WRITTEN: docs/IMPACT.md (the 12 points mapped; ONE model - pressure x = the defender's tiredness 1 - f + 0.25 x the mismatch beyond an even band of 1.41 (both men's current points^0.5, the blow's impact against his guard - shield right / parry / WRONG side / none -, both skills) + 0.75 while he reels from an impact stagger; each outcome = Anton's ceiling (kick: shield 90%, down 80%; right block / parry: stagger 80%; wrong side 90 / 50 / 40; clean hit 80 / 50; riposte 50) x an S-curve of x from its own start; lengths x (1 + extra x T) with a roll that widens with T: stagger 0.6 s fresh -> 1.05-1.95 s empty, kick reel up to x2.5, get-up up to x2; stun-lock guards: 2 s / 5 s immunity, each outcome halves the next for 8 s; worked examples computed by script - even fresh men 0 always, the 200-at-100% king vs the 50-at-20% man 90 / 80 one way and 0 the other, the peasant's stick vs the berserker's axe 46% stagger fresh, the player 100 cannot kick-win a fresh 220 king but breaks him at 10%; ~60 named parameters; build order I1 SPIKE (prove each engine lever with loud fixed values) -> I2 SHADOW mode (decide + log only) -> I3 kicks -> I4 blocks -> I5 clean hits -> I6 riders / feedback / guide -> I7 the AI punishing a window - working names, numbered only on Anton's yes since step 46 is already queued; 14 open questions with recommendations + what a combat designer would add: from behind, armour, run speed, missiles) and RESEARCH §R + UNVERIFIED 27-33 (file + line in the v1.4.8 decompile). KEY FINDS: vanilla builds a blow for EVERY collision and asks the damage model's Decide* (KnockDown / KnockBack / ShrugOff / Dismount) but never REGISTERS a blocked one (Mission.cs 5368) - a blocker can only be knocked down by a blow of our own (RegisterBlow is public; vanilla's drowning blow is the precedent) or a crush-through; a 0-damage blow plays no reaction (Agent.cs 5438); behaviours see a landed blow only BEFORE the engine reacts and read-only, so the decorator is the one managed place; a kick never knocks down in vanilla (null weapon) but always knocks back; CalculateDefendedBlowStunMultipliers lengthens the blocker's and the attacker's stun by ref (capped 0.8 s by a public managed parameter) and runs in a MULTI-THREAD-callable callback; act_stagger_* exist unused (RBM crashed a horse with one); the knockdown is one fall-and-rise action (slow it = slower get-up); DropItem leaves a pickable shield, the AI re-picks shields only with no enemy within 20 m. NO HARMONY needed. Nothing in DESIGN / defaults.json / the BUILD ORDER yet - Anton answers IMPACT §8 first. (2026.10.17 22.13.21)

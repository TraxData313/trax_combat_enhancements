# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

**Trax Combat Enhancements** (working title) — a combat mod for *Mount & Blade II:
Bannerlord* v1.4.8 that makes fights a bit more fun: every landed hit rolls ±50% damage,
and every fighter has an **Athletics** bar — his stamina, as big as his Athletics skill —
that blows drain and rest refills. The top quarter of his own bar is full strength; below
it his damage upside, attack RATE and run speed fall, down to one attack in five when empty -
since step 13 PAUSE ONLY: the animations play at full speed and a no-attack timer of
D × (1/m − 1) follows each attack (the player's input gated; the AI's attack bits taken out of its
own input - guard really up - since step 16, NoAttack before; the player sees an Attack recovery bar
above his Athletics bar); wounds cap the bar; tired AI fighters step back out of the press after a
swing (step 5d) - since step 16 a BACKPEDAL through the AI's own input, facing the enemy, and the
pause survives it (one per-man `AgentComponent` on `OnAIInputSet` does both; A/B switches keep the old ways).
Heroes and party leaders pay less per blow (and big-skill heroes have
big bars), so the game leans hero-centred. The Athletics bar is shown for the player (step 6)
and — averaged, with a ± spread and the men's health — in a strip under each formation card of
the orders menu (step 9). The bar for the fighter you look at and squad bars above the
formations are LATER (DESIGN §3; their settings wait in DESIGN's "Planned parameters"). Words:
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
  `[damage]`, `[athletics]`, `[speed]`, `[rate]`, `[stepback]`, `[hud]`, `[error]`). Always logged: mod/game version
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
docs/REVIEW.md                the code reviews: step 10a (R1-R24) and step 17's second one over steps 12-16
                              (R25-R36) - every finding with its severity, place, scenario and status (fixed
                              in which commit / not a bug / deferred / For Anton)
docs/PLAYTEST.md              THE script of Anton's one playtest session (step 10b: one ordered run,
                              parts A-G, ~2 h; appendix L1-L8 = every log line and summary block)
src/TraxCombat.Core/          netstandard2.0 — pure logic, no game refs, unit-tested:
  ParamDef.cs                 one setting: key, type, range, group, label, plain-words
                              description, apply timing (Live / NextBattle); Normalize, Format;
                              its Default comes from defaults.json (DefaultsFile), never code
  SettingsSchema.cs           EVERY setting of DESIGN's table (70), in file + MCM order, 9 groups
                              ("Master switch" first, "Advanced" last; step 10b's one vocabulary and
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
                              FormatVersion 3 + Migrate (step 13: a format-1 file's old defaults -
                              AttackRateAiDecisions true, PlayerBarOffsetBottom 54 - get the new ones once;
                              step 14: a format-2 file's MinMoveSpeedMultiplier 0.3 → 0.7 once)
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
                              DamageCategory, DamageSkipReason
  DamageStats.cs              per-mission roll stats (kinds, min/avg/max, before → after, avg per
                              hit, dice histogram, skips by reason, mod-OFF hits unrolled, the
                              upside by the attacker's f + rolls above their top, errors per
                              site, thread) + the [summary] text; thread-safe
  Athletics.cs                DESIGN §2 pure (Athletics v2, step 5c): AthleticsRules (live;
                              Enabled = ModEnabled && AthleticsEnabled, OffBecause, the floors),
                              Fighter (state as a FRACTION of the FULL pool, AthleticsSkill,
                              Health, the three applied multipliers), AthleticsMath - ONE function
                              per rule (PoolPoints = max(floor, per-skill × skill), UsableFraction
                              = the health cap, PeakShare = f, Attack/Run/MountSpeedMultiplier =
                              floor + (1 − floor) × f, DamageUpside, BlowCostPoints in points,
                              RegenRateMultiplier by effort, SpeedUpdateNeeded (0.05 step), PeakBin;
                              step 14's refill curve: RegenCurve = 1 − (1 − k) x, RegenRateAtEmpty r0 =
                              ln(1/k) / ((1 − k) T), RefillFrom / RefillSeconds - exact per step)
                              + Charge / ApplyHealth / Regen / Read; BlowKind, BlowOutcome,
                              RegenOutcome, AthleticsReading (HUD snapshot incl. f, usable pool,
                              BelowFull = below the top it can refill to - step 12)
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
                              ≥ 0.1 s, AnimationMultiplier = max(m, min%), CountdownText "1.3 s" (tenths
                              up), FlashOn - 2 pulses, the recovery bar's colours), PlayerAttackTimer
                              (YOUR timer: the hold from the release's start, the countdown, Frame =
                              the input gate's decision - clear / swallowed / flash / ended / held at
                              the end, Release), PlayerGateFrame, PlayerTimerEnd, AttackRecoveryReading
                              (the Attack recovery bar's read: Share, SecondsText, the flash)
  AttackRateStats.cs          per-mission attack rate: melee / ranged x AI / you x f band - the
                              animation asked, every phase, the cycle, m, the target, measured ÷
                              target + verdict; the TIMER rows (D, m, the pause asked, the measured
                              gap to the next attack, after its end, early starts; step 16: the timer's
                              floor D/m and the cycle against it); left-out counts (mixed, beyond the
                              cap, cancelled, chained; step 16: cycles with a step back inside are
                              COUNTED and shown apart per band); your timer (presses swallowed,
                              flashes, held-button fires, missed attacks, releases); the AI timer (by
                              input / by NoAttack, kinds, mounted, reasons, ends); the guard by f;
                              the AI-decision recomputes; the [summary] "attack rate" lines
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
                              settings), StripFallback, OrderStripStats (+ the [summary] strip line)
  SpreadStats.cs              MeanStd (Welford, population std), FormationAthleticsStats (squad
                              mean ± std, band, mean f, at full strength, mean health - step 9),
                              RunSpeedCheck (engine top / asked / moving by f); 5c's
                              attack-interval classes went in 5e
  AthleticsStats.cs           per-mission Athletics counters + the [summary] text (pools from the
                              skill, blows by kind, riders, detection cross-checks, free actions,
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
                              the file rewritten as format 2) and on the MCM-save re-read
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
                              ApplyGeneralDamageModifiers only: BaseModel first, then the roll
                              (our exceptions → the game's value)
  Models/DamageRandomizer.cs  feature 1, game side: game structs → HitFacts → Decide → roll with
                              the attacker's f (the rider's on a horse charge) as the upside,
                              stats, [damage] lines (mission start, first roll + thread,
                              verbose roll/skip), the [summary] damage block
  Models/TraxAgentStatModel.cs AgentStatCalculateModel DECORATOR — forwards everything;
                              UpdateAgentStats: base first, then the attack ANIMATIONS × max(m,
                              AttackAnimationMinPercent) (step 13: 100 = full speed), the run
                              multiplier (+ the AI's attack values while AttackRateAiDecisions,
                              5e - off by default since 13), or a slowed rider's horse's
                              (SpeedFactorsFor - managed reads only); + the tournament
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
                              OnMissileHit), the health cap (OnAgentHit + every regen step), regen
                              by effort, three speed multipliers re-targeted in 0.05 steps and
                              applied by UpdateAgentProperties (≤ 50 a tick), the horse table
                              (OnAgentMount/Dismount), hot swap, SpeedFactorsFor (the decorator's
                              lookup), the run-speed and walk/run sampling, Failed
  Missions/AthleticsLogic.Api.cs  READ API for steps 6-9: TryGetReading(agent) (points, pool,
                              usable pool, f, peak line, multipliers), TryGetPeakShare(agent),
                              TryGetFormationStats(formation) (incl. mean f and health; the player
                              left out, as the order cards), FormationStatsVersion, IsRunning
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
                              the smoke drives; FlagNames)
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
                              it covered), the never-called warning, the first hold's input frames
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
  Missions/PlayerAttackGate.cs  step 13: a MissionLogic added then moved to INDEX 0 of
                              Mission.MissionBehaviors (SubModule, OnMissionBehaviorInitialize) - it
                              pre-ticks right after MissionMainAgentController wrote the input
  Missions/PaceBody.cs        PaceState (+ D, kind, the attack's end, the pause); IPaceBody = the AI
                              timer's ENGINE side (the smoke plays it); GamePaceBody: NoAttack via
                              SetScriptedFlags only on a free man (no GoToPosition / NoAttack / object
                              / ladder / detachment; riders allowed since 13), lifted only while he is
                              free, else Waiting; step 16: InputPaceBody (no flag - hooked, the same
                              refusals, never waits; release = the callback off if idle); PaceState +
                              ByInput, Overlapped, Deferred, the call count at the start, hits taken
  Missions/TrackedAgent.cs    one fighter's record: Core Fighter + detection, f-bin, fresh top
                              speed and slowed-horse fields, his StepBackState (null until needed),
                              the attack-rate phase state, the running attack's D and its end, his
                              last rest by kind, his last timer (for the gap), his PaceState, his
                              AiInputState (step 16, null until first held by input)
  Missions/AthleticsLogic.Hud.cs  step 6: AttachHud on the logic's FIRST TICK (the screen runs by
                              then - RESEARCH §G) - each view via MissionScreen.AddMissionView with
                              a GauntletHudLayer, "[hud] attached:" lines; WriteHudSummary (the
                              [summary] hud: lines + each view's own; the screen finalizes views
                              before it). Attached: the player bar, the orders strip (step 9, with
                              its GauntletOrderCards + MissionStripFormations)
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
                              grace clock; builds / removals outside a fight in the "hud-outside" bucket)
  Hud/HudLayer.cs             HudFrame (one frame as a view sees it; IsFightMode = Battle, Duel,
                              Tournament, Stealth; step 12: IsWalkMode = StartUp, PlayerWeaponDrawn by
                              HandsFull; OrderMenuOpen), IHudLayer (the engine seam), GauntletHudLayer
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
  Hud/OrderCards.cs           step 9's engine seams: IOrderCardSource / GauntletOrderCards (the
                              "MissionOrder" layer found with the public FindLayer, its widget tree
                              walked once per open - card = OrderTroopItemBrushWidget in a two-widget
                              slot - and each card's pixels / visibility / CurrentMemberCount read
                              every frame; nothing patched), IStripFormations / MissionStripFormations
  Hud/OrderStripView.cs       step 9, DESIGN §3.4: the orders-menu strip - while the menu is open,
                              a cell under each card (placed every frame in pixels, slot k checked
                              against formation k), the fallback panel (reasons, 0.5 s / 1 s grace),
                              values every refresh, [hud] first placement / card changes / fallbacks
                              / values lines, the [summary] strip line
  Hud/OrderStripVM.cs         its ViewModels: the root + 8 fixed OrderStripCellVM in one
                              MBBindingList, bound TWICE by the prefab (the cells, the panel rows)
tests/TraxCombat.Core.Tests/  net8.0 xUnit (367) — schema vs DESIGN.md (keys + types), one copy
                              runs (SingleCopyTests: claims on private slots, copies, texts), when
                              MCM is tried and when it stops (McmPlanTests - step 12),
                              defaults.json (DefaultsFileTests), master switch, settings, config
                              file, merge rule, rate limiter (+ Peek), the log trim (LogTrimTests:
                              kept kinds, one cut point, stacks, notes, the last resort, bytes),
                              damage roll/rules/dice/stats/upside,
                              Athletics v2 rules (pool, f, cap, curves, effort regen, the refill curve
                              - T for any k, 100 = the old rule to the bit, exact steps - DESIGN's
                              blow counts), mean/std, the run-speed check, Athletics summary, the
                              attack rate (rules, AI values x / ÷ m, the cap, verdicts, phases /
                              cycles / timers / holds / guard + summary), the timer (AttackTimerTests:
                              the pause, the animation floor, the countdown text, the flash, YOUR
                              timer as the gate drives it, the recovery read), the config migration,
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
                              the lift, texts, band, health in the squad stats, summary). They
                              run on DESIGN's INITIAL values (DesignTable.cs: a module
                              initializer), so tuning defaults.json never breaks them. Keep green.
module/SubModule.xml          release manifest (Id TraxCombatEnhancements, v0.1.0) - THE one home of the
                              version (the DLLs read it at build; stamped once, on release day)
module/GUI/Prefabs/           the HUD movies - file name = movie name, `Trax…` (prefab names are
                              global across modules): TraxPlayerAthleticsBar.xml (step 6: native
                              sprite BlankWhiteSquare_9 + brush AgentHUD.Interaction.Text only;
                              FillBarWidgets draw shares; no widget takes mouse events),
                              TraxAttackRecoveryBar.xml (step 13: the label, a frame, the fill, the
                              seconds centred on it, the flash overlay - same native sprite / brush),
                              TraxOrderStrip.xml (step 9: {Cells} ItemTemplates - the cells placed
                              by Scaled* pixel bindings, the panel rows; the ± band = a FillBarWidget
                              ChangeWidget; a list widget carries no @binding - its own bindings
                              would resolve against the list)
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
tools/WORKSHOP-UPLOAD.md      the release loop, step by step (first upload, updates) + the uploader's quirks
tools/WorkshopCreate.xml      the FIRST upload (creates the item, Private, tags, preview) - run once
tools/WorkshopUpdate.xml      every later upload - ITEM_ID placeholder until the first upload
tools/STEAM-DESCRIPTION.bbcode  the Workshop page, pasted by hand (≤ 8000 bytes; 4427 now)
tools/preview_thumbnail.html  the Workshop preview (source) → preview_thumbnail.png (1024², headless Edge)
tools/AssemblyGuard/          soft-dependency guard (from the sibling): MCM, Harmony, ButterLib,
                              UIExtenderEx, NavalDLC, CustomBattle in any type surface = FAIL
tools/OfflineSmoke/           the real DLL on .NET Framework with the game's DLLs, no game
                              launched: types load without MCM, config flows, tournament fix,
                              damage (Program.Damage.cs: the real decorator over the game's
                              CustomAgentApplyDamageModel, fed the game's own hit structs),
                              Athletics (Program.Athletics.cs: the real stat decorator, damage
                              decorator and AthleticsLogic on uninitialized Agent objects - the
                              curves blow by blow, DESIGN's blow counts, the health cap, the
                              upside by f, horses), the step back (Program.StepBack.cs: the real
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
                              the bar and the strip removed too), the recovery bar (Program.Recovery.cs:
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
                              ONE decorator of each kind and reports once (52 steps; the config
                              checks work for any tuned default);
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
- Game install: `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord`.
  Workshop mods (MCM = 2859238197, maybe RCM) under `...\steamapps\workshop\content\261550\`.

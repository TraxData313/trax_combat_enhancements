# AI notes

Claude's detail companion to TASKS_TODO.md — one section per BUILD ORDER step. Read the
step's section before starting it; leave it richer than you found it.

---

## Step 2 — Research (no code)

Goal: every hook the mod needs, verified against the **v1.4.8** decompiled game
(`..\reference\game-decompiled\`, decompile more assemblies beside it when needed), written
to `docs/RESEARCH.md` — per question: the verified answer with source file references, the
recommended approach, the risks. If something in DESIGN.md turns out infeasible or costly,
say so there and propose the nearest good alternative.

Questions:

- **A. Damage** — where is a landed hit's FINAL damage (after armor) computed, for people
  and horses, melee and missiles? Is it a game model (AgentApplyDamageModel?) registered in
  BOTH the campaign and custom battle, and can we DECORATE it (does v1.4.8 hand us the
  previously registered model)? If not, the cleanest Harmony point.
- **B. Detecting a blow** — the cheapest reliable signal that a fighter RELEASED an attack,
  landed or not: melee swing/thrust, bow, crossbow, throw — for AI and player alike, with
  1000 agents. And the signal for a LANDED blow (incl. one that hit a shield / was parried).
- **C. Slowing attacks** — which AgentDrivenProperties govern melee swing, thrust, bow draw,
  crossbow reload, throw speed; where they are computed (AgentStatCalculateModel?); how to
  apply a temporary ×0.2 to ONE agent and remove it again, surviving the game's own
  recomputes (weapon switch, etc.). Does the engine clamp the multiplier?
- **D. Movement** — agent speed on foot and on horseback for the standing/moving regen rule.
- **E. Hero / party leader** — from an Agent: is it a hero, and is it the leader of its own
  party (campaign: agent origin → party → LeaderHero). What to do in custom battle.
- **F. Mission lifecycle** — adding our MissionBehavior to every combat mission (field,
  siege, hideout, arena/tournament, custom battle); agent spawn/remove events; cheapest
  per-agent state storage.
- **G. HUD** — how vanilla shows the player's health (VM + prefab) and how we add our own
  bar near it (own GauntletLayer in a MissionView? no UIExtenderEx if avoidable); world →
  screen projection for bars floating above formations; which agent the player is
  looking/aiming at; the orders-menu formation cards (VM + prefab) and whether adding to
  them needs UIExtenderEx (+ Harmony/ButterLib) — cost of that dependency vs alternatives.
  Is RCM in the Workshop folder? Where/how its posture bar sits (style reference only).
- **H. Config + MCM** — MCM v5's fluent builder (verify the API against the MCM DLLs in
  Workshop item 2859238197) used from method bodies only, so the mod loads WITHOUT MCM;
  compare with the sibling bridges (`..\ImmersiveAI`, `..\TrainingBattlesMod\...\Mcm`).
  Config file format: is Newtonsoft.Json shipped with the game, and can it read a JSON file
  with `//` comments (the instructions beside each value)?
- **I. Harmony** — is it needed at all after A–H? Prefer not.

Answered in `docs/RESEARCH.md` (A–I, plus J hot swap and K logging, added mid-step at
Anton's request). Verdict: no Harmony, no UIExtenderEx; 13 design implications for Anton at
the top of RESEARCH.

## Step 3 — Scaffold (DONE 2026-09-27)

**Built** (file map in CLAUDE.md "Layout"): solution + `Directory.Build.props` (`*.user`
import); Core (schema of all 32 DESIGN settings, `TraxSettings.Shared`, config-file text,
merge rule, edit tracker, rate limiter) + 53 xUnit tests; Module `TraxCombatEnhancements.dll`
(SubModule, ModPaths, ConfigStore, TraxLog, McmBridge, two pass-through decorators,
AthleticsLogic stub); `module/SubModule.xml` v0.1.0; `tools/deploy.ps1` (build → AssemblyGuard
→ OfflineSmoke → install as `Modules\TraxCombatEnhancements.Dev`); `tools/AssemblyGuard`;
`tools/OfflineSmoke` (16 checks, see below). PLAYTEST §1 written.

**How later steps use it**
- Read settings AT USE TIME: `TraxSettings.Shared.DamageRandomPercent` etc. (typed property per
  key). Never copy into a field at mission start. To rebuild on change: subscribe
  `TraxSettings.Shared.Changed` (raised on the setter's thread, main thread in practice) or
  compare `TraxSettings.Shared.Version`.
- A NEW setting = a row in DESIGN's table + an entry in `SettingsSchema` (right group, right
  place — file and MCM order follow it) + a typed property on `TraxSettings` + (since step 5b)
  `"Key": value` in defaults.json, then the DefaultsTool refresh. `SchemaTests` and
  `DefaultsFileTests` fail until all four agree; the file and the MCM page pick it up by themselves; an old
  config.json gets the new key written in at the next read (logged).
- Log: `TraxLog.Info(tag, msg)`; chatty lines `if (TraxLog.VerboseOn) TraxLog.Verbose(tag, msg)`
  (check VerboseOn FIRST so the hot path builds no strings); `TraxLog.Error("area.hook", e)` in
  every catch. Tags in TraxLog's class doc.
- Summary: `AthleticsLogic.WriteSummary` has the marked spot for step 4/5 lines.
- Offline checks: add to `tools/OfflineSmoke/Program.cs` whatever can run without the game
  (it already loads the game's DLLs on .NET Framework; pure managed game types can be
  constructed, e.g. `new TraxAgentStatModel(...)`).

**Decisions**
- **THE FILE-REWRITE RULE** (`Core/ConfigMerge.cs`, tested + smoke): when the mod writes
  config.json while one exists (MCM Done; adding missing keys), it RE-READS the disk file at
  that moment; per setting — changed in MCM since the last write → memory's value; else a
  valid value on disk → the disk's value (a hand edit made while the game runs survives, and
  applies at the next battle start); else memory. Unknown keys carried in a trailing "Not
  recognised" section. "Changed in MCM" = live value differs from the value before the first
  MCM edit (`EditTracker`), so a slider moved and moved back, or MCM's Cancel, never overwrites
  a hand edit. A disk file that does not parse → copied to `config.json.broken-<time>`, then
  written fresh from memory. At a read, the file is rewritten only when keys are MISSING and
  nothing is invalid (an invalid hand value is left for the player to see in the log). Own
  `//` comments are not preserved (the header says so). Writes via temp file + replace.
- **Reads**: startup, every game start/load (`OnGameStart`), every mission start
  (`OnMissionBehaviorInitialize`, before AthleticsLogic attaches). Missing/invalid key →
  default. Before a re-read, MCM edits that never reached the disk are written first.
- **MCM "default" preset kept and filled with DESIGN's defaults** (RESEARCH §H said remove
  it — wrong: MCM.UI's page Reset and per-setting reset apply the preset with id "default";
  without it Reset silently does nothing). `CreatePreset("default", …)` BEFORE `BuildAsGlobal`
  wins because MCM's preset builder keeps the first value per key. Verified offline.
- **No lambdas with MCM-typed parameters** (they compile to a cached static field typed on MCM
  → the load trap; RESEARCH §H's sample would have failed the guard — proven with a probe
  DLL). Builder callbacks are instance methods `Fill(object)` passed as `Action<object>`
  (contravariance). The built settings are held as `object`. Guard: 0 errors, 0 warnings.
- Float settings stored rounded to 4 decimals (MCM's 0.1f → 0.1); ints rounded; all as
  doubles in one array. Ranges chosen per setting (see `SettingsSchema`), e.g.
  `ExhaustedAttackSpeedPercent` 5–100 (0 could freeze an animation), `ExhaustedRecoverPercent`
  0–90, `MaxAthletics` 10–1000. Every setting is `ApplyTiming.Live` (a test enforces it; a
  NextBattle one must be a deliberate exception).
- `ConfigVersion: 1` stamp in the file (meta, not a setting) — bump + migrate if a later step
  must change the meaning or push a new default into existing files (every key is written, so
  a changed default does NOT reach an existing file otherwise).
- Error handling: every game hook try/catch → `[error]` with stack, rate-limited per place
  (3 stacks, then one per 10 s, counts reported); a red in-game line at most every 30 s
  (queued, shown from `OnApplicationTick` = main thread). Verbose: 40-line burst, 20/s per tag.
- RBM (manager commit f7dc70c, DESIGN §5): `[compat]` line at every load; if module id `RBM` or
  `RBM_*` is enabled, ONE yellow message per session (main menu, else game start).
- Deploy ships the `.pdb` files (line numbers in `[error]` stacks), never Newtonsoft.Json (the
  game's own 13.0.0.0 is used; Core compiles against the 13.0.1 package — same identity).

**Gotchas**
- `TaleWorlds.Engine.Path` clashes with `System.IO.Path` — alias it (`using Path = System.IO.Path;`).
- MCM needs `0Harmony` at run time (its wrappers use HarmonyLib extensions) — in game the
  Bannerlord.Harmony module provides it; the offline smoke resolves it from Workshop 2859188632.
- MCM's `ISettingsPropertyGroupBuilder` keys properties by DISPLAY NAME: labels must be unique
  within a group (tested). Group names must not contain `/` (sub-group delimiter; tested).
- `ProxyRef<T>` silently ignores a value of the wrong CLR type (`value is T`): bool/int/float
  exactly — the preset boxes each default as its setting's type.
- The informational version carries `+<git commit>` of HEAD at build time (SDK SourceLink):
  the `[load]` line says which build Anton runs — deploy AFTER the final commit.
- Windows PowerShell 5.1 runs `deploy.ps1`: keep it ASCII; manifest read/written with explicit UTF-8.

**UNVERIFIED — only the game can tell (PLAYTEST §1 has the lines that prove each)**
1. The mod loads in the launcher/game, with MCM and without (GetTypes() without MCM passes
   offline on .NET Framework with the real game DLLs; the real module loader is not run).
2. `EngineFilePaths.ConfigsPath` resolves to `Documents\Mount and Blade II Bannerlord\Configs`
   in game (fallback path logged if not).
3. MCM registration timing: `BaseSettingsBuilder.Create` non-null at the main menu or within
   the 1 s retries; the page appears in Mod Options with 7 groups / 32 settings (since 5b: 8 groups / 33
   settings + the "Defaults" buttons) (built and
   inspected offline through MCM's real builder + discoverer, but not the Gauntlet UI).
4. MCM opened from the Escape menu MID-BATTLE edits live values; Done raises SAVE_TRIGGERED
   in game (the container path is decompiled, not run); slider drags log every step.
5. The two decorators register over the expected base models in campaign, custom battle and
   War Sails games, and vanilla combat is unchanged (pass-through).
6. The tournament AI-level fix in a real tournament (mechanics verified offline).
7. `OnEndMissionInternal` → `[summary]` for every ending (victory, defeat, retreat, leaving a
   town scene); `Mission.CombatType` / `Mode` values at AfterStart.
8. `Utilities.GetModulesNames()` at OnSubModuleLoad (module list + RBM detection).
9. `InformationManager.DisplayMessage` at the main menu (load line, RBM line, error line).

## Step 4 — Damage randomness (DONE 2026-09-27)

**Built** (file map in CLAUDE.md "Layout"): Core `RandomSource.cs` (IRandomSource,
ThreadSafeRandom, SeededRandom), `DamageRoll.cs` (HitFacts, DamageRules, Decide, Factor,
Apply, Roll, GameRound), `DamageStats.cs` (per-mission stats + summary text) + 39 tests (92
total, incl. a 200k-roll distribution check: bounds, mean, flat histogram). Module
`Models/DamageRandomizer.cs` + `TraxDamageModel.ApplyGeneralDamageModifiers` (the only
non-forwarding member); `AthleticsLogic.AfterStart` → `DamageRandomizer.OnMissionStart()`,
`WriteSummary` → `DamageRandomizer.WriteSummary()` (own try). `TraxLog.Verbose(tag, msg,
bucket)` overload. OfflineSmoke +6 checks (22) in `Program.Damage.cs`. PLAYTEST §2.

**The flow per hit** (main thread): `BaseModel.ApplyGeneralDamageModifiers` (outside our try —
its exceptions are the game's own) → `HitFacts` from `cd.IsColliderAgent`,
`ai.IsVictimAgentNull`, `cd.IsFallDamage`, `cd.AttackBlockedWithShield`, `cd.IsMissile`,
`ai.IsVictimAgentMount`, `cd.IsHorseCharge` → `DamageRules.From(TraxSettings.Shared)` (live) →
`Decide` → skip (counted, verbose line in bucket `damage-skip`) or `Roll` (counted, first roll
per mission always logged with its thread, verbose line otherwise) → the game rounds our value.

**Decisions**
- **Skip order** (first match wins, each counted by reason): object (not an agent collider) →
  no victim → fall → the game's value rounds to 0 → master switch → spread 0 → shield toggle →
  mount toggle → melee/ranged toggle. Target toggles (shield, mount) and attack toggles
  (melee/ranged) COMBINE: an arrow into a horse rolls only with Ranged AND OnMounts on.
- **Category** for the stats is exclusive: shield → mount → ranged → melee. Horse charge =
  melee (follows `DamageRandomMelee`). Kicks/bashes (`IsAlternativeAttack`) = melee, rolled.
  Missile area damage and siege-engine shots at people = ranged (they are `IsMissile`).
  Friendly fire rolls too (a landed strike); its verbose line says "friendly fire".
- **Rounding rule, precisely**: the game does `(int)Math.Round(x)` (banker's: 0.5 → 0, 2.5 → 2).
  "0 stays 0" = if the game's value would SHOW as 0 (x ≤ 0.5) we return it untouched; else
  `max(1, x·f)`. Factor = 1 − p + 2p·u with u ∈ [0,1) → [1 − p, 1 + p); the top is never quite
  reached (fine). p = 100 can draw ~0 → floor 1.
- **Errors**: our part throws → the game's value; `DamageStats.AddError(site)` is true only the
  first time per site per mission → that one goes to `TraxLog.Error` (stack + red in-game line),
  the rest are counted in the summary ("damage roll errors: N (damage.roll N)"). Sites:
  `damage.roll` (the hook), `damage.log` (building a log line — the roll still stands),
  `damage.mission-start`, `damage.summary`.
- **Thread**: verified from source — every hit callback is `[MBCallback(null, false)]`
  (not multi-thread callable) → main thread (RESEARCH §A updated). Still: dice per thread,
  stats under one uncontended lock, and every roll compares its thread with the one
  `OnMissionStart` ran on → summary "ran on the main thread: all N" or "OFF the main thread".
- **No new parameters.** The histogram's 10 slices and the verbose bucket name are log
  plumbing, not gameplay numbers.
- Implications 2–4 of RESEARCH (knockdown follows the roll; shields not rolled by default;
  charge = melee, fall/objects never) are already in DESIGN §1 and Interpretation 9; the code
  follows them. DESIGN unchanged in this step.

**Gotchas**
- `AttackCollisionData` has private fields; offline it is built with the game's public
  `AttackCollisionData.GetAttackCollisionDataForDebugPurpose(...)` (IsHorseCharge =
  ChargeVelocity > 0, IsFallDamage = FallSpeed > 0). `AttackInformation` has public fields —
  `new AttackInformation { IsVictimAgentNull = …, IsVictimAgentMount = … }` works offline.
  `CustomAgentApplyDamageModel.ApplyGeneralDamageModifiers` returns its input → a clean base
  for offline checks (`model.Initialize(base)` sets BaseModel).
- The verbose limiter is keyed by bucket: a flood in one bucket (the smoke's flood check)
  empties it for ~2 s — the smoke sleeps 1 s before its verbose checks.
- The game's combat log already shows "Extra damage from skills, perks and effects: N" /
  "Reduced damage…" whenever `CalculateDamage` changes the value (`combatLog.ModifiedDamage`) —
  our roll shows up there, mixed with perks.
- `Agent.Name` allocates (TextObject.ToString) — only in the verbose/first-roll path.

**UNVERIFIED — only the game can tell (PLAYTEST §2 has the lines)**
1. The engine calls our `ApplyGeneralDamageModifiers` for every landed hit in campaign,
   custom battle and naval battles (`[damage] first roll this mission` line).
2. Hit callbacks on the main thread (source says so; the first-roll line proves it).
3. Shield HP: the native `Agent.OnShieldDamaged(slot, inflictedDamage)` uses the collision's
   (rolled) InflictedDamage — only matters with `DamageRandomOnShields` on.
4. Object hits (gates, siege engines) really reach the model as `!IsColliderAgent` (counted as
   "objects") — source says `GetAttackCollisionResults` runs for them with damage > 0.
5. Names in the verbose line for the player, mounts (`horse X of Y`) and missiles (the missile
   item) read well in game.
6. Combined with other damage mods (only RBM is declared incompatible): the summary's before →
   after is the tool.

## Step 5 — Endurance core (DONE 2026-09-27; renamed Athletics in step 5b - names below are the new ones)

**Built** (file map in CLAUDE.md "Layout"): Core `Athletics.cs` (AthleticsRules, Fighter,
AthleticsMath), `SpreadStats.cs` (MeanStd, FormationAthleticsStats, IntervalStats,
SpeedVerdict), `AthleticsStats.cs` (per-mission counters + summary text) + 30 tests (122).
Module: `AthleticsLogic` as 4 partial files (lifecycle / Engine / Api / Log), `TrackedAgent`,
`Models/SpeedPenalty.cs`, `TraxAgentStatModel.UpdateAgentStats` (the one non-forwarding
change), `TraxLog.Limited`. OfflineSmoke +6 checks (28) in `Program.Athletics.cs`. PLAYTEST §3.

**The flow** (all main thread):
- Spawn: `OnAgentBuild` → `Track` (humans only; mounts never) → hero / leader flags cached.
  First tick: `SweepAgents` picks up anyone spawned before us.
- Tick (`TickAthletics`): settings version changed → `ApplySettingsChange`; for every tracked
  active fighter one native `GetCurrentActionType(1)` → on a change `ObserveAction` (rising edge
  into ReleaseMelee = `StartRelease` → `Charge`; falling edge = swing length; ReleaseRanged /
  ReleaseThrowing / Kick / WeaponBash only counted); `SpeedDirty` → `UpdateAgentProperties()`;
  every 0.1 s `RegenPass`; every `FormationStatsRefreshSeconds` `RefreshFormationStats`.
- Events: `OnMeleeHit` (couched/braced charge, the release check, landed-only melee),
  `OnAgentShootMissile` (ranged charge, 0.1 s dedupe, remembers the missile index),
  `OnMissileHit` (landed-only ranged).
- `Charge` → Core `AthleticsMath.Charge` → stats → `RetargetSpeed` (new multiplier? mark
  dirty) → `OnExhausted`. Regen → `OnRecovered` (retarget) / `OnRefilled`.
- Decorator: `BaseModel.UpdateAgentStats` → `AthleticsLogic.SpeedMultiplierFor(agent)` (1 for
  untracked / no mission / AthleticsEnabled off) → `SpeedPenalty.Scale`.

**Decisions**
- **State = FRACTION of the pool** (`Fighter.Fraction`, 1 = full): "MaxAthletics change keeps
  each fighter's fraction" holds by construction; points = fraction × `PoolPoints`.
- **One pure function per rule** (manager's heads-up for Athletics v2, DESIGN §2b):
  `AthleticsMath.PoolPoints(r, f)`, `BlowCostPoints`, `RegenFractionPerSecond(r, f, speed,
  topSpeed)` (top speed already passed, unused by §2), `AttackSpeedMultiplier` (a float per
  fighter), `IsExhausted`. 5c changes those bodies, not the plumbing.
- **Speed = a float per fighter** (`Fighter.SpeedMultiplier`) that the decorator applies on
  EVERY recompute. The logic re-targets it on a charge, a recovery or a setting change and asks
  for `UpdateAgentProperties()` only when it moved by `SpeedUpdateStep` (0.02) or to/from 1 —
  §2's cliff flips twice per exhaustion; 5c's line will move in small steps.
- **`UpdateAgentProperties()` only from the tick**, never inside an engine hit callback (not
  proven unsafe - just not risked): ≤ 1 frame late.
- **Poll every tick, not throttled**: release phases of fast weapons can be short; the cost is
  measured (`Athletics tick cost`). Fallback if it shows: `AgentComponent.OnTickParallel`.
- **Regen every 0.1 s**, only fighters below full or exhausted (a full fighter costs nothing, no
  native call). The integration is exact (only the part of the step after the delay counts); the
  moving sample and recovery are 0.1 s coarse. Moving = `(MountAgent ?? agent).MovementVelocity`.
- **Recovery is checked every regen step** even without a gain, so a lowered
  `ExhaustedRecoverPercent` applies at once.
- **Hit before the poll saw the release**: `OnMeleeHit` reads the action itself and runs the same
  `ObserveAction`. A hit counts "in a counted release" if we were in one until now (the engine may
  already be in the blocked/parried reaction) or the action is a release now.
- **Landed-only melee** (`CostOnMiss` off): first hit on an agent per swing, keyed on the swing
  counter (`ReleaseSerial` vs `LandedSerial`); a hit outside any release charges at most once per
  `BlowTimeSeconds` (counted as "time fallback"). A release charged in on-miss mode marks its
  serial, so switching modes mid-swing never charges twice.
- **Landed-only ranged**: the shooter's own last 4 missile indices (`MissilesList`'s last entry at
  `OnAgentShootMissile` - the game adds it right before calling behaviours); siege-engine
  missiles never get there; a multi-penetration missile charges once.
- **Couched lance / braced spear**: `IsDoingPassiveAttack` in `OnMeleeHit`, victim ≠ null, at
  most one per `BlowTimeSeconds` (a couch through two men = one blow). Horse-charge bumps
  (`IsHorseCharge`) and kicks / bashes (`IsAlternativeAttack`) are free.
- **AthleticsEnabled off mid-battle**: `ResetFull()` everyone + retarget (penalties lifted on the
  next tick); the decorator ALSO checks the flag live (fail safe). On again: everyone full.
- **Leaders**: campaign = `(Origin.BattleCombatant as PartyBase).LeaderHero.CharacterObject ==
  agent.Character`; no campaign = `BattleCombatant.General == agent.Character`, a side without a
  general → its heroes. A leader who is not a hero (defensive) gets the leader factor alone.
- "Athletics spent" = points really drained (a swing at 0 drains nothing).
- **The attack-speed measurement** (the engine-clamp question): melee time between releases,
  ranged time between shots, and the length of swings that hit NOTHING (purest - a hit cuts the
  animation short); each classified by the penalty at both ends (a change in between → left out);
  0.05 s histogram up to 30 s; verdict "ARE slower" at ≥ x1.5 the fresh median.
- Always-on lines: the player's own exhaustion / recovery / back-to-full and each party leader
  at spawn (`TraxLog.Limited`, own buckets); everyone else's in verbose buckets
  (athletics-blow, -exhaust, -regen, -hero, speed-update).
- **No new parameters.** 0.1 s regen step, 0.1 s shot dedupe, 0.02 speed step, 30 s interval
  cap, x1.5 verdict are engine/log plumbing. DESIGN unchanged.

**Gotchas**
- **Agent's static initializer calls the engine** (`DefaultTauntActions` → `ActionIndexCache` →
  `MBAnimation`). Offline: never touch an Agent STATIC field; `FieldInfo.SetValue` on an Agent
  INSTANCE runs the type initializer too (and breaks the type for the process) - the smoke sets
  backing fields with IL `stfld` (`FieldSetter`). `FormatterServices.GetUninitializedObject`
  is fine.
- **`Agent.IsActive()` and `GetMaximumForwardUnlimitedSpeed()` are raw pointer reads**
  (`AgentHelper`): on an uninitialized agent = access violation (uncatchable crash). Offline
  paths avoid them (`WriteAthleticsSummary` samples speeds only with a Mission).
- `ActionCodeType`, `FormationClass` and `DrivenProperty` have ALIAS values - `ToString()` may
  print `AttackMeleeAllBegin` / `NumberOfDefaultFormations`. Names come from a fixed table
  (formations: "1 Infantry"… as the game numbers them) or the first declared field.
- Sandbox's and CustomBattle's `UpdateHumanStats` assign the three speed values with `=` on
  every recompute → scaling after the base never compounds. A base model that multiplied instead
  would compound - the first-exhaustion log would show a ratio ≠ the asked one.
- There is NO native getter for driven properties: "the penalty is in the agent's properties"
  proves the managed side (what we send); only the timing check proves the engine uses it.
- .NET Framework (the game, the smoke) formats midpoints differently from .NET 8 (the tests):
  the smoke asserts text prefixes around such numbers.

**For step 5c** (manager asked; verified in source, values UNVERIFIED in game) - DONE in step 5c,
see its section (kept for the trail)
- (Step 5b) Names are Athletics now; new keys go into defaults.json as well (see "Step 5b"); the
  run-speed and damage-upside levers must respect `r.Enabled` (= ModEnabled && AthleticsEnabled)
  and give back vanilla speed / damage when it is false.
- Top speed: `agent.GetMaximumForwardUnlimitedSpeed()` - pointer read, cheap; for a rider use
  the mount's. Speed: `(MountAgent ?? agent).MovementVelocity.Length` (native, m/s).
- Walk speed: `agent.WalkSpeedCached` (managed) = the mount's `WalkingSpeedLimitOfMountable`
  (native) or `Monster.WalkingSpeedLimit` - `Modules\Native\ModuleData\monsters.xml`: human
  1.8 m/s (human_settlement 1.4, child 1.6). The summary's `speeds for step 5c` line measures
  walk vs top on foot and for horses every mission, plus a histogram of speed ÷ top speed seen
  while refilling (tenths, "above 1" = faster than the "top"). That gives `WalkEffortFraction`.
- Run-speed lever: driven properties `MaxSpeedMultiplier` (87) / `CombatMaxSpeedMultiplier` (88)
  in the same decorator (same pattern as the attack speed). `Agent.SetMaximumSpeedLimit` is the
  formation movement's own lever - avoid it.
- Per-fighter pool (Athletics, DESIGN §2b as revised in 11edf4f): cache the skill on the record at
  `Track` (e.g. `GetEffectiveSkill(agent, DefaultSkills.Athletics)` through the stat model) and
  make `AthleticsMath.PoolPoints(r, f)` read it; the fraction store already keeps each share when
  the pool changes. Health cap: clamp `Fraction` to health left in the regen step and on a hit.
- Damage upside from the ATTACKER's Athletics: the damage decorator can call
  `AthleticsLogic.TryGetReading(attacker, out var r)` (the rider for a horse charge) - main
  thread, allocation-free.

**UNVERIFIED — only the game can tell (PLAYTEST §3 has the steps; the line that settles each)**
1. The engine honours a 0.2 multiplier (no clamp), bows and crossbows included without the
   `Bipedal*` values → `[summary] attack speed check, melee - …` / `swing length` / `ranged` say
   `ARE slower` (x3-x5); `[speed] first exhaustion …` shows the managed values took x0.20.
   (Step 5e replaced those lines: read the `attack rate, … empty (f 0)` rows - wind-up / swing /
   draw / reload about x5.00.)
2. Every swing, mounted too, shows as `ReleaseMelee` on channel 1 → `Athletics detection: …
   during a counted release A, outside one B [in action: …]` with B ≪ A; `(mounted Y)` > 0.
3. Polling ~1000 agents per tick is cheap → `Athletics tick cost: avg … ms`.
4. `OnAgentShootMissile` once per shot → `shots seen N (+E extra projectiles …)` ≈ `ranged
   releases seen by the poll`.
5. Kicks / bashes on channel 1 → `Athletics free: kicks K, shield bashes B, kick/bash hits H`
   (K = 0 with H > 0 = another channel; free either way).
6. Couched lance: `IsDoingPassiveAttack` is true at the landed hit → `couched/braced hits`.
7. Leader flags in campaign, tournaments and custom battle → `[athletics] party leader: …` lines
   and `Athletics heroes: … party leaders (…)`.
8. `GetCurrentActionType(1)` inside `OnMeleeHit` is meaningful → the `[in action: …]` list.
9. Our stat model is on top in every mission type → `[speed] stat model on top in this mission: ours`.
10. The horse's speed drives riders' regen → `Athletics regen: … moving`.
11. `Mission.MainAgent` is set by `OnAgentBuild` (only the "(you)" in the leader list depends on it).

## Step 5b — Athletics rename, defaults.json, master switch (DONE 2026-09-27)

**Built**
- **Rename** (Anton: "it is the Athletics bar that gets depleted"): keys `EnduranceEnabled` →
  `AthleticsEnabled`, `MaxEndurance` → `MaxAthletics`; group "Endurance" → "Athletics"; types and
  files `EnduranceLogic(.Api/.Engine/.Log)` → `AthleticsLogic`, `EnduranceRules/Math/Reading/Stats`
  → `Athletics*`, `FormationEnduranceStats` → `FormationAthleticsStats`, `Endurance.cs` →
  `Athletics.cs`, the tests, smoke `Program.Athletics.cs`; log tag `[athletics]`, buckets
  `athletics-*`, error sites `athletics.*`, summary lines "Athletics …". Words: the bar / pool /
  points = "Athletics"; the character-screen skill = "the Athletics skill". Code renamed by a
  verified script (tag/bucket/site rules, then Endurance → Athletics), docs by hand. No aliases
  for the old keys (nothing released; the loader reports unknown keys as typos).
- **defaults.json** (DESIGN §2c) - the one truth for default values; Core `DefaultsFile`,
  `tools/DefaultsTool`, 21 tests. MCM buttons *Revert all to defaults* / *Save current values
  as a defaults file*; the config header says how to revert without MCM.
- **Master switch `ModEnabled`** (Anton's scope addition mid-step, DESIGN §4): first setting, own
  first group; Core `ModSwitchLog`; 11 tests; a smoke step.
- 154 tests (122 → +21 defaults, +11 master switch), OfflineSmoke 31 checks.

**The one-truth design for defaults**
- `defaults.json` (repo root) → `EmbeddedResource` in Core (LogicalName
  `TraxCombat.Core.defaults.json`; a missing file is a build error on purpose) →
  `DefaultsFile.DefaultFor(key, type, min, max)`, called by the `ParamDef` constructor while
  `SettingsSchema`'s static fields initialise. The schema declares NO default values; everything
  else reads `ParamDef.Default` (new TraxSettings, first-run file, missing key, MCM preset/Reset,
  the revert button, "(default …)" in comments/hints/log).
- **Static-init trap**: the embedded parse runs INSIDE SettingsSchema's type initializer, so it
  must not touch SettingsSchema - it keeps a raw `JToken` per key; unknown-key detection is lazy
  (`DefaultsFile.Problems`, read after the schema exists).
- **Fail safe**: a key missing / unusable in the embedded file → bottom of its range (false for a
  switch); out of range → clamped; all listed in `Problems` → `[config]   defaults.json PROBLEM:`
  lines at load; the smoke fails on any.
- **Tests run on DESIGN's Default column**, not on defaults.json: `DesignTable.cs` has a
  `[ModuleInitializer]` that hands DESIGN's values to `DefaultsFile.UseValuesForTests` before any
  test touches the schema. So Anton tuning defaults.json never breaks a test that counts blows
  (verified: DamageRandomPercent 40 / CostPerBlow 8 / VerboseLogging true → all green, smoke too).
  The smoke runs the real embedded defaults: its config checks read the default and pick
  different hand-edit values; the Athletics smoke sets DESIGN's numbers explicitly.
- DESIGN's Default column = INITIAL value; SchemaTests compare keys + types only.
- `DefaultsFileTests`: keys both ways (exact casing), strict JSON types (true/false; an integer
  token for Int; any number for Float), inside the range, ≤ 4 decimals, the // lines = what
  `DefaultsFile.Write` produces with values blanked (number formatting is free), the embedded
  copy = the repo file, and `ResolveAll` (the load path) finds no problem.
- `dotnet run --project tools/DefaultsTool -- refresh` rewrites comments / headings / order and
  keeps every value; refuses while a key is missing, unknown or bad. `check` reports only.
- **A new setting** = DESIGN row (initial value) + schema entry (no default) + TraxSettings
  property + `"Key": value` anywhere in defaults.json + refresh. Tested workflow (ModEnabled).

**Master switch `ModEnabled`**
- Damage: `DamageRules.ModEnabled`; `Decide` returns `ModOff` LAST (after every other rule), so
  an OFF battle counts exactly the hits an ON battle rolls; `DamageRandomizer` keeps the game's
  value and calls `DamageStats.AddVanilla` → summary "damage while the mod was OFF … avg N per
  hit" (the ON line gained ", avg N per hit"; a mixed battle adds "damage avg per hit: rolled
  (mod ON) … | mod OFF …").
- Athletics: `AthleticsRules.Enabled` = ModEnabled && AthleticsEnabled (+ `OffBecause`), so every
  existing `r.Enabled` gate (poll, hits, shots, regen, read API) covers it; the switch-off path
  (`ApplySettingsChange`) refills everyone and lifts penalties; `SpeedMultiplierFor` checks
  ModEnabled itself (vanilla even before the next tick). Back on = everyone full (Anton's choice,
  kept: a fresh start).
- Mission: `ModSwitchLog` in AthleticsLogic - `[mission] start: … mod ON|OFF`, a `[mission] mod
  switched OFF (ModEnabled) at 42.3 s - …` line per toggle (observed each tick), summary header
  "mod ON" / "mod OFF" / "mod was on for N% of the battle (started ON; OFF at …)". The logic and
  both decorators stay attached while off (logging + the vanilla-keeping tournament AI fix).

**MCM buttons** - verified in MCMv5 **5.12.3** (the Workshop DLL = `..\reference\MCMv5-5.12.3-decompiled`)
and MCM.UI for 1.4.8 (`Bannerlord.MBOptionScreen.v1.4.8.dll`, decompiled for this step):
- `ISettingsPropertyGroupBuilder.AddButton(id, name, IRef, content, Action<ISettingsPropertyButtonBuilder>)`;
  the button builder is an `ISettingsPropertyBuilder<>` (SetOrder / SetRequireRestart / SetHintText).
- Click = `SettingsPropertyVM.OnValueClick`: `if (PropertyReference.Value is Action a) a();` → our
  `ProxyRef<Action>(getter, null)`; getters are instance methods returning a static handler.
- Presets: BuildAsGlobal stores the button's current value (the Action) in the "default" preset;
  applying a preset writes through the setter - null → ignored.
- Page refresh after a revert: every SettingsPropertyVM subscribes to the settings object's
  PropertyChanged and re-reads its value for any name but SAVE_TRIGGERED → `RefreshPage` raises
  "TRAX_VALUES_RESET" via `BaseSettings.OnPropertyChanged`.
- The revert is not in MCM's undo stack (URS): Cancel does not undo it (hint says so). It writes
  config.json itself with EVERY key from memory (`WriteMerged(everyKeyFromMemory)`), so a hand edit
  waiting in the file is overwritten too - "revert all" means all.
- Export: `DefaultsFile.Write(Settings.Snapshot())` → `<config dir>\defaults.json`, the repo file's
  exact layout (copy it over as is; `DefaultsTool check --file <path>` checks it).

**Gotchas**
- Git Bash `sed -i` rewrote a CRLF file as LF - edit defaults.json with the Edit tool or the tool.
- An XML comment cannot contain `--` (MSB4025) - the refresh command lives in C#, not the csproj.
- `[ModuleInitializer]` in a library raises CA2255 - suppressed with a pragma and a reason.
- Check parses with `DuplicatePropertyNameHandling.Error` (strict); the embedded load with
  `Replace` (robust). core.autocrlf=true: the repo stores LF, the build embeds CRLF on Windows;
  the tests compare without line endings.
- ConfigStore: a config.json deleted while the game runs now means "every default" at the next
  re-read (was: the values in effect) - what the header promises.

**UNVERIFIED — only the game can tell (PLAYTEST §4, §5 have the lines)**
1. The two buttons render and click in MCM's real UI (page built by MCM's real builder offline;
   the click path read in the MCM.UI decompile and simulated in the smoke).
2. The page re-reads its values after a revert (the PropertyChanged wiring, same decompile).
3. The green/red InformationManager line shows while the options screen is open.
4. Toggle times: `Mission.CurrentTime` at the first tick after MCM closes.
5. An OFF battle really plays as vanilla, no penalty left over (`[athletics] the whole mod
   (ModEnabled) switched OFF … penalties lifted` + the feel of it).

## Step 5c — Athletics v2 (DONE 2026-09-27)

DESIGN §2 is now the one current spec (§2b folded in; a one-line §2b pointer stays because
TASKS_TODO's 5d line cites it).

**Built** (file map in CLAUDE.md "Layout")
- Settings (38): + `AthleticsPoolFloor` 50, `AthleticsPoolPerSkill` 1.0, `AthleticsPeakPercent` 75,
  `HealthCapsAthletics`, `MinMoveSpeedMultiplier` 0.3, `MountMinSpeedMultiplier` 1.0,
  `DamageBonusFollowsAthletics`, `RegenMultiplierAtFullRun` 0.5, `WalkEffortFraction` 0.4;
  retired `MaxAthletics`, `FullRegenSecondsMoving`, `MovingSpeedThreshold`,
  `ExhaustedRecoverPercent` (schema, TraxSettings, defaults.json + refresh, DESIGN table, MCM by
  construction). Group "Exhaustion" → "Tired fighters"; MCM label "Attack speed when empty (%)".
- Core: `AthleticsMath` v2 (PoolPoints, UsableFraction, PeakShare, Curve → Attack/Run/Mount
  multipliers, DamageUpside, RegenRateMultiplier / IsWalking / Effort, ApplyHealth, PeakBin);
  `DamageRoll.Upside` + `Factor(u, p, upside)`, `RollOutcome.Upside/Spread/Ceiling`;
  `DamageStats` upside bins; `AthleticsStats` rewritten; `BinnedIntervals`, `RunSpeedCheck`;
  `FormationAthleticsStats.MeanPeakShare/InPeak`.
- Module: the engine (skill at spawn, health cap, three multipliers, mount table, recompute
  budget, effort regen, run-speed sampling), `SpeedPenalty.ScaleRun/ScaleMount`, the stat
  decorator's `SpeedFactorsFor`, the damage decorator's attacker f, `TryGetPeakShare`, new log lines.
- Tests 154 → 207; OfflineSmoke 31 → 33 checks. PLAYTEST §3 rewritten; RESEARCH §C/§D addenda.

**Decisions**
- **Fractions and points (the bookkeeping)**: `Fighter.Fraction` = share of the FULL pool; the
  pool is computed LIVE from the cached skill (`max(floor, per-skill × skill)`, never below 1).
  So a pool-setting change keeps shares by construction, and a blow's POINT cost lands as
  cost ÷ pool at that moment (a recruit's 10 = 20% of 50, a legionary's 10 = 7.7% of 130).
  f = min(Fraction ÷ peak%, 1) does not even need the pool. The health cap is a ceiling on the
  same fraction (health left, 0..1), so "the peak line stays on the full pool" holds by
  construction. Lowest/points in the summary = fraction × the pool at the end.
- **The change threshold**: `AthleticsMath.SpeedUpdateStep` = **0.05** per applied multiplier
  (attack, run, horse; step 5 had 0.02 for the cliff) + any move to/from exactly 1 or onto/off
  exactly the floor; a refill that reaches its top sets the speeds exactly. At most
  `MaxRecomputesPerTick` = **50** `UpdateAgentProperties()` a tick (fighters + horses); the rest
  wait a tick (`held a tick by the per-tick budget`). Estimate: a refill from empty to the peak
  ≈ 16 recomputes per fighter; a blow below the peak ≈ 1 (small blows on big pools every 2-3).
- **Walk/run evidence** (RESEARCH §D addendum): human walk limit 1.8 m/s (monsters.xml, the
  formations' own walk gait = min `WalkSpeedCached`); run = native top speed, inferred ≈ 6.2
  (`bipedal_speed_multiplier`) × MaxSpeedMultiplier (0.63-0.79 for typical troops) = 3.9-4.9 m/s
  → walk/top 0.37-0.46, ~0.42. **`WalkEffortFraction` stays 0.4**: above it the rate falls in a
  line, so 0.42 still refills at 98%. The summary's `walk vs run speeds` line measures it.
- **Run lever = `MaxSpeedMultiplier` only**; `CombatMaxSpeedMultiplier` is a ≤ 1 share the base
  clamps (scaling both would square the penalty in combat stance). **Horse lever = `MountSpeed`**
  on the mount's own properties, via a mount table (horse index → rider record, reference-checked
  with `SlowedMount`), so the decorator stays managed-only (offline-safe). `OnAgentMount/Dismount`
  mark the rider; `ApplyMountSpeed` recomputes the old horse (back to 1) and the new one.
- **The skill**: `agent.Character.GetSkillValue(DefaultSkills.Athletics)` at `Track` (heroes via
  `HeroObject`), NOT the stat model's `GetEffectiveSkill` (captain perks etc. would make the bar
  wobble with the formation). Exception or no character → 0 = the floor, counted "(not readable)".
- **Health**: managed `Health ÷ HealthLimit` (limit ≤ 0 → full) at every hit (`OnAgentHit` - after
  the health drop) and every regen step. Healing raises the cap, only regen refills. Athletics
  (or the mod) back on = everyone full, the next regen step cuts to the cap (counted as cuts).
  Health 0 is skipped: `OnAgentHit` fires for the killing blow BEFORE the death, and a whole bar
  "cut" per kill would swamp the summary's biggest cut.
- A slowed rider leaving the field: his horse leaves the mount table at once (the decorator
  reads 1 for it) and is recomputed by the next tick (`ReleaseHorses`, within the budget) -
  never `UpdateAgentProperties` inside `OnAgentRemoved`.
- **Speeds are sampled every regen step** for fighters below their top (not only after the
  delay): the run-speed check needs fighters that are fighting. Effort seconds count only while
  refilling.
- **Damage**: attacker f via `TryGetPeakShare` (f = 1 while Athletics or the mod is off); a mount
  attacker → its `RiderAgent`; no tracked attacker → NaN → the full upside, own "no pool" bin.
  `DamageRules.UpsideFollowsAthletics` (optional last ctor arg) = `DamageBonusFollowsAthletics`.
  Every roll carries its ceiling 1 + p × upside; the summary counts rolls above it (must be 0).
- **Binned attack checks**: an interval is classified by the f bin after the previous release's
  charge (what governs it) vs before the next; a swing length by the bin after its own charge;
  different bins = "mixed", left out. Verdict: the tiredest bin with ≥ 3 samples (empty, else
  below 0.5) vs the peak (≥ 5): ≥ x1.5 slower when ≥ x1.5 asked = ARE slower.
- **Run speed check**: per fighter a fresh top (`GetMaximumForwardUnlimitedSpeed` while his run
  multiplier is 1, refreshed on every such sample); per f bin the mean engine top ÷ fresh, the
  mean asked, speed ÷ fresh (p90 = the 0.05 slice's UPPER edge, max). "follows the curve" =
  |engine − asked| ≤ 0.05 in every bin with ≥ 20 samples and asked < 0.97. Horses alike (the
  mount's fresh top); with MountMin 1.0 the verdict is "unaffected, as asked".
- **First-exhaustion proof**: with the curve the fighter is already slowed before his emptying
  blow, so the "before" snapshot carries the previous multipliers; the check is new ÷ old per
  value; "of his fresh values" = snapshot ÷ previous multipliers (`FirstFresh`).
- Player lines (always, Limited, bucket athletics-player): YOU pool at the first tick, dropped
  below full strength, exhausted, off empty, back at full strength, back to full / to the
  wound's cap, wounded. Verbose buckets: athletics-pool, athletics-health (+ the step-5 ones).
- A consequence of DESIGN's "effort = speed ÷ CURRENT top speed", kept as written: a near-empty
  man's top (x0.3 ≈ 1.3 m/s) is below a formation's walk (1.8 m/s), so keeping up is a flat-out
  run for him - half-rate regen until he has some speed back. DESIGN §2 says so.

**Gotchas**
- Float settings: 0.4f is 0.4000000059… - tests compare curve values to 6 decimals.
- IntervalStats medians are bin CENTRES (x.x25 / x.x75): 2-decimal text is a rounding midpoint,
  formatted differently on .NET 8 and .NET Framework - tests assert around them.
  `RunSpeedCheck` p90 reports the slice's upper edge instead; 0.30 / 0.05 = 5.999… in doubles
  (an epsilon in the binning).
- Offline smoke: fake agents have no Character → skill 0 → the floor (50); health set through
  `_health` and `<HealthLimit>k__BackingField` by IL; the mount table filled by reflection
  (`RegisterMount`). `ApplyMountSpeed`, `RegenPass`, `SampleSpeeds` call native members - not
  reachable offline.
- Git Bash python edits of .cs files keep LF (autocrlf warnings only); markdown only via Edit/Write.

**UNVERIFIED — only the game can tell (PLAYTEST §3; the line that settles each)**
1. The skill read (heroes' real skill, troops' data) → `[athletics] YOU: Athletics skill N → pool …`
   = the character screen; no `(not readable)`; summary `Athletics pools … whose skill could not be read` absent.
2. The engine honours the attack curve at mid values, not only 0.2 → `attack speed check … by f`
   rows with x ≈ asked and `- tired attacks ARE slower`. (Since step 5e: the `attack rate, …`
   band rows - each phase's (x…) against the peak ≈ 1/m.)
3. `MaxSpeedMultiplier` honoured live and `GetMaximumForwardUnlimitedSpeed()` follows it →
   `run speed check, on foot … - the engine's top speed follows the curve`, `moving p90` falling.
4. Horses: `MountSpeed` honoured, the mount table and OnAgentMount/Dismount → with MountMin 0.5
   `[speed] first horse slowed this mission …` and the horses line "follows the curve"; at 1.0
   `unaffected, as asked`.
5. The cap at the hit (`OnAgentHit` after the health drop) → `[athletics] YOU are wounded … capped at …`,
   summary `Athletics health cap: N cuts`.
6. Effort units (MovementVelocity vs top speed) and the walk ratio → `walk vs run speeds … walk/top`
   and `Athletics refill effort … seconds per tenth` (walking near 0.4, running near 1, little above 1).
7. A horse charge reads the rider's f → verbose `[damage] horse charge on … (x…, attacker f …)`.
8. Recompute cost with 1000 agents → `speed updates: N recomputes …, held a tick … M` and
   `Athletics tick cost`.
9. The first-exhaustion check with previous multipliers → `[speed] first exhaustion this mission: …
   - the penalties are in the agent's properties`.

## Step 5d — Tired fighters step back (research 2026-09-27)

Pointers from 5c (kept): f = `AthleticsMath.PeakShare(in r, st)` / `TryGetPeakShare`; chance =
`StepBackMaxChancePercent` × (1 − f); the swing edges are `StartRelease` / `EndRelease` in the
engine's poll; dice = Core `IRandomSource`; master switch first; new keys the step-5b way.

**Research - how the game moves ONE AI man** (v1.4.8 decompile; RBM's `RBMAI.dll` decompiled to
`..\reference\RBMAI-decompiled` for prior art)
- Two separate native states. The FORMATION FRAME is rewritten every agent tick:
  `Agent.TickParallel` → `HumanAIComponent.ParallelUpdateFormationMovement` → `GetFormationFrame`
  (`Formation.GetOrderPositionOfUnit`) → `Agent.TrySetFormationFrame` → `SetFormationFrameEnabled`,
  plus `AdjustSpeedLimit` → `SetMaximumSpeedLimit` (so a speed limit of ours would be overwritten
  every tick). The SCRIPTED FRAME is `Agent.SetScriptedPosition(ref WorldPosition, addHumanLikeDelay,
  AIScriptedFrameFlags)` / `SetScriptedPositionAndDirection(ref pos, radians, delay, flags)` →
  native; it sets `AIScriptedFrameFlags.GoToPosition` (1) (the naval AI tests that bit right after
  calling it) and lasts until `Agent.DisableScriptedMovement()`. The formation code never writes
  the scripted frame, and nothing in vanilla calls DisableScriptedMovement on formation men
  periodically (every caller listed: StopUsingGameObject, ladder/climbing detachments, ballista,
  HumanAIComponent's own timer, sandbox behaviours). So a scripted step is NOT overridden by the
  formation the next tick, and the formation frame (kept current underneath) takes the man back
  the moment the scripted one is disabled.
- Vanilla does exactly this to formation men MID-BATTLE: item pickup (`HumanAIComponent.ItemPickupTick`
  → `MoveToUsableGameObject` → `SetScriptedPositionAndDirection(…, NoAttack)`; done →
  `StopUsingGameObject` → `DisableScriptedMovement`), ladder queues (`LadderQueueManager`), duel
  spectators (`HideoutMissionController`). `HumanAIComponent` even has an UNUSED timed variant,
  `SetScriptedPositionAndDirectionTimed(pos, radians, seconds)` - its `OnTick` counts the seconds
  down and calls `DisableScriptedMovement()` - the engine's own "stand there for N s" shape.
- `NoAttack` (2) is an ADDITIONAL flag, so scripted men fight by default (the flag would be
  pointless otherwise): the combat AI (target, defence) keeps running under a scripted frame.
  Item pickup clears its NoAttack by `DisableScriptedMovement()` alone and those men attack again
  afterwards → the additional flags belong to the scripted frame. (Flags set with
  `SetScriptedFlags` directly - `UseGameObject`'s NoAttack, StandingPoint's `DisableScriptedFrameFlags`
  - are cleared by hand; not our path.) `DoNotRun` (0x10) = walk (ScriptedMovementComponent).
  `ConsiderRotation` (4) is what a direction adds (StandingPointForRangedArea clears it after use).
- **Vanilla's own gate**: `Agent.CanBeAssignedForScriptedMovement()` = active, AI-controlled, NOT
  detached from the formation (siege engines, ladders, towers, strategic areas, attack-entity
  detachments), NOT running away, NO GoToPosition already, NOT using / moving to / defending a
  game object, NOT in a ladder queue. Item pickup, banner bearers, UsableMachine.AddAgent and
  StrategicArea.AddAgent all check it - so while OUR GoToPosition is set, they skip our man and
  nothing vanilla takes him mid-step.
- Facing: no managed evidence either way (the native AI decides). The combat AI keeps its target
  under a scripted frame; `SetScriptedPositionAndDirection`'s direction (radians,
  `Vec2.RotationInRadians` = atan2(−x, y)) sets the facing the frame asks for; `DoNotRun` keeps it
  a walk. `SetLookAgent` is head-look for conversations and cinematics only (never battle AI) -
  not used. **UNVERIFIED**: whether a man backing up under a scripted frame keeps facing his enemy
  (and keeps blocking) - the logs measure it (below).
- Navmesh: `Agent.CanMoveDirectlyToPosition(in Vec2)` (native; item pickup's own precondition)
  says a straight walk to the spot is clear (walls, fences, parapets, narrow gaps).
  `WorldPosition` from `agent.GetWorldPosition()` + `SetVec2` keeps the agent's navmesh face as the
  start; `GetNavMeshZ()` is NaN off the navmesh and gives the height of the spot on the navmesh
  → a wall edge (the spot resolves to the ground below) shows as a height step.
- Mission kinds: tournament and arena fights run in `MissionMode.Battle` too
  (TournamentFightMissionController, ArenaMasterCampaignBehavior) - they are told apart by their
  mission behaviours (`TournamentBehavior`, `ArenaPracticeFightMissionController`), by type NAME
  (no SandBox reference needed). Duels: `MissionMode.Duel`. Naval: `Mission.IsNavalBattle` /
  `IsNavalRaidBattle` (moving decks; the naval AI component scripts swimming men itself).
- Formation orders: `formation.GetReadonlyMovementOrderReference().OrderEnum` (Charge 2, Move 7,
  Retreat 8, Stop 9, Advance 10, FallBack 11 …), `formation.ArrangementOrder.OrderEnum`
  (Circle 0, Column 1, Line 2, Loose 3, Scatter 4, ShieldWall 5, Skein 6, Square 7).

**Alternatives weighed**
- **RBM's BackStep** (Realistic Battle Mod, `Frontline.cs`): a Harmony prefix on
  `Formation.GetOrderPositionOfUnit` that returns a spot 0-0.3 m behind the man plus
  `Agent.SetTargetPosition`, re-decided at every query, charge orders only, skipped when the spot
  is occupied. Needs Harmony (this mod has none - RESEARCH §I) and patches formation code (RTS
  Camera Command System patches it too); RBM is declared incompatible anyway. Not taken - but it
  is proof that shoving front-line men back a little is playable.
- **Behaviour values** (`agent.SetAIBehaviorValues(Melee, y1, x2, y2, x3, y3)` →
  `HumanAIComponent.OverrideBehaviorParams`, pushed in `OnTickParallel`): piecewise weights by
  distance; vanilla's `DefensiveArrangementMove` melee curve (4, 5, 0, 20, 0) is "fight only what
  reaches you". Clean, supported, restored by the formation itself (`RefreshBehaviorValues` on
  every order change; `OverrideBehaviorParams` marks the set Overriden so the next refresh wins).
  But it is "hang back", not a visible step back. **The fallback if the playtest shows turned
  backs** (the nearest thing that works).
- `SetMaximumSpeedLimit` - overwritten by the formation every tick. No.
- **Rank swap** `Formation.SwitchUnitLocations(a, b)` (vanilla: once, BannerBearerLogic) - the
  literal "the fresh step in", but `LineFormation.SwitchUnitLocations` rebuilds the unit list
  (`ReconstructUnitsFromUnits2D`), line-type arrangements only, meaningful only while holding. A
  later idea, not per swing.
- The vanilla TIMED helper: it would release the man even if our logic died - but its timer keeps
  running if the game hands the man a new scripted job, and would then cancel THAT job. Our own
  timer instead: the logic's tick never stops while the mission runs, and every exit path
  releases (below).

**Choice: the literal spec** - after a melee swing ENDS (the falling edge out of ReleaseMelee - the
swing completes first; "after each melee swing"), roll `StepBackMaxChancePercent` × (1 − f); on a
hit, from the NEXT TICK (never inside an engine hit callback): `SetScriptedPositionAndDirection` to
the spot `StepBackDistance` straight away from the man's current target, facing the target,
flags `DoNotRun` (+ `NoAttack` while `StepBackHoldAttacks`) - hold it `StepBackSeconds`, then
`DisableScriptedMovement()` and the formation takes him again.

**Safety - never starts** (each counted by reason in the summary): mod / Athletics / step-back
off; f = 1 (chance 0 - no roll at all in the peak zone); the dice; the player (`IsMainAgent`), not
AI-controlled; riding; anything but a melee swing (shots, throws, kicks, bashes, couched lances
never reach the swing edge); not `MissionMode.Battle`, tournaments / arena, naval battles, the
mission ending; `!CanBeAssignedForScriptedMovement()` (detached, on a ladder or in its queue, at a
siege engine, using or walking to an object, running away, already scripted - incl. already
stepping back); routing (`IsRetreating`); the formation in shield wall, square or circle (they
exist to hold - Claude's call); the formation ordered to retreat; no target, a target that is not
an active enemy, or farther than `StepBackEnemyRange`; the spot off the navmesh, more than 1 m
higher or lower (a wall edge, stairs), or not reachable in a straight line; `StepBackMaxAtOnce`
already stepping back; more than 20 starts in one tick (plumbing, like the recompute budget); no
HumanAIComponent.

**Always released**: time up; the man leaves the field (no engine call on a removed man); the
mission ends (released through the engine before the summary; the teardown fallback only drops
the records); settings off - ModEnabled, AthleticsEnabled or StepBackEnabled - releases EVERY man
at once on the next tick; his formation's movement order or arrangement changes; he changes
formation; the player takes him over; he mounts; he routs; the game hands him a job of its own
(object, ladder queue - then we drop our record WITHOUT disabling, so we never cancel the game's
job); an exception in our code for him (release attempted).

**Built (DONE 2026-09-27)** - file map in CLAUDE.md "Layout"
- Settings (45, group "Tired fighters step back" after "Tired fighters"): `StepBackEnabled`,
  `StepBackMaxChancePercent` 100, `StepBackDistance` 2.0, `StepBackSeconds` 1.5 (the four
  planned rows) + `StepBackEnemyRange` 4.0, `StepBackHoldAttacks` true, `StepBackMaxAtOnce` 50.
- Core `StepBack.cs` (rules, math, reasons), `StepBackStats.cs` (+ summary) - 19 tests (226).
- Module `AthleticsLogic.StepBack.cs` (bookkeeping, logs), `StepBackBody.cs` (the engine side
  behind `IStepBackBody`); hooks: `EndRelease` (the roll), `StartRelease` (swings while
  stepping), `TickAthletics` end (`TickStepBacks`, every tick whatever the switches), `Untrack`
  (left the field), `OnMeleeHit` top (the guard count), `WriteAthleticsSummary` start
  (`CloseStepBacks`) and end (the 8 lines), first tick (`NoteStepBackMission`).
- Smoke: `Program.StepBack.cs` (a new step, 34 steps) + the master-switch step releases a
  running step back. deploy.ps1 green (guard OK, smoke OK, installed).

**Decisions**
- **The roll comes at the swing's END** (the falling edge out of ReleaseMelee; the poll or a hit
  callback sees it) - "after each melee swing", and the swing always completes. f = after that
  swing's cost. f 1 → chance 0 → no dice drawn, but the roll is COUNTED (the peak row of the
  summary must read 0%). Player / riders / non-battle missions are "not rolled", counted apart,
  so the f rows hold only men who could step back.
- **Queue, then the tick**: a yes is queued (`StepBackState.Pending`) and started by
  `TickStepBacks` at the end of the same or next tick - never inside an engine callback (the
  step-5 rule for `UpdateAgentProperties`, kept for scripted movement too).
- **Our own timer, not vanilla's** `SetScriptedPositionAndDirectionTimed`: its timer would
  later cancel a scripted job the game gave the man meanwhile. Time is read LIVE
  (`StepBackMath.TimeUp`), so a shorter StepBackSeconds applies to running ones.
- **The game's reasons before "time up"** in the same tick (`Check` first): a man handed to a
  game job is never disabled over it.
- **Hand-over vs detach**: ladder queue / using an object / moving to one = the game's own
  scripted job → record dropped, NOT disabled. Detached without such a job → released (a
  scripted frame nobody tracks would never end).
- **Flags**: we pass `DoNotRun` (+ `NoAttack`); `OurFlags` = those (+ `ConsiderRotation`, which a
  direction adds) that were not set before. After `DisableScriptedMovement` any of them still
  set is cleared by hand and counted (`our flags … cleared by hand`) - expected 0 (item pickup
  proves the engine drops them with the frame).
- **Engine did not take it** (GoToPosition not set right after the call) → disabled again at
  once and counted `engine did not take the scripted position`.
- **Mission kind** once per mission by behaviour TYPE NAME ("Tournament", "Arena" - no SandBox
  reference), naval by `IsNavalBattle / IsNavalRaidBattle`; live: `Mode == Battle`,
  `!MissionEnded`, `!IsTeleportingAgents` (a scripted man would be TELEPORTED then).
- **Mission end**: `CloseStepBacks` at the start of the Athletics summary releases everyone
  through the engine (agents still live at `OnEndMissionInternal`); the teardown fallback only
  drops records. After it no new step back can start (`_stepClosed`).
- **"In formation vs loose"** = the formation's movement state at the start: holding (Hold /
  StandGround) vs charging (Charge) vs no formation.
- **Plumbing constants** (not gameplay numbers, like steps 5/5c's): `MaxHeightStep` 1 m,
  `MaxStartsPerTick` 20, facing bins at cos ±0.5 (60° / 120°), moving at 0.2 m/s, "reached" at
  0.35 m, the mid-step sample at half the time, "overdue" = time + 1 s.
- Hideout boss fights run in battle mode: a tired boss may step back from the player (not
  excluded; PLAYTEST 6d asks Anton).

**Gotchas**
- The smoke's fake agents have no native side: everything the step back asks the ENGINE is in
  `IStepBackBody`; the logic itself reads only managed members (`IsMainAgent`, `MountAgent` =
  `_cachedMountAgent`, `Name`). The smoke mounts a fake rider by writing `_cachedMountAgent` with
  IL (never reflection on an Agent - the static initializer).
- `GameStepBackBody.MissionAllows(null)` is false, so the older smoke steps (no Mission) never
  roll - their "not a field battle" count is the proof.
- A summary CLOSES the step back for the logic (`_stepClosed`); the smoke's master-switch step
  reopens it by reflection after the summary step.
- `SetScriptedPositionAndDirection`'s managed wrapper drops the native bool result - the
  GoToPosition flag right after the call is the only "taken" signal.

**UNVERIFIED - only the game can tell (PLAYTEST §6; the line that settles each)**
1. A scripted man keeps FACING his enemy while he backs up (THE risk) →
   `[summary] step back facing … mid-step (N sampled): facing his enemy X (…%), side-on …, back turned Z`
   (Z near 0) and the first one's `mid-step facing his enemy (N° off), moving away (… m/s away)`.
2. The engine takes the scripted position on a formation man mid-melee →
   `first step back this mission: … (GoToPosition set: the engine took it)`; summary
   `not started … engine did not take the scripted position` absent or small.
3. The formation takes him back after `DisableScriptedMovement`, nothing lingers →
   `release check: … still on right after 0 (must be 0), our flags … cleared by hand 0 | … overdue 0 … 0`.
4. He keeps his guard (blocks) while scripted with NoAttack →
   `guard: hits taken while stepping back N - blocked B (P%) … everyone else on foot … (Q%)`, P not far below Q.
5. NoAttack holds his swings → `swings started while stepping back 0`.
6. He actually moves (walk speed, tired run curve) → `moves: avg X m of 2.00 asked` (0.8-1.5 expected).
7. The navmesh checks (height, straight way) are right on walls and stairs → siege summary
   `spot not level`, `no straight way back`, and nobody seen stepping off a wall.
8. Tournament / arena behaviour names → `[stepback] this mission is a tournament or arena fight (<TypeName>) …`.
9. `CanBeAssignedForScriptedMovement` keeps ladders / siege engines / pickups out → `busy (…) N` in sieges.
10. Formations keep their shape with 50 at once → Anton's eyes + `most at once`.
- **If 1 fails** (backs turned): the fallback is behaviour values - `agent.SetAIBehaviorValues`
  (Melee kind) toward vanilla's DefensiveArrangementMove curve (4, 5, 0, 20, 0) scaled by f,
  restored by `agent.RefreshBehaviorValues(movementOrder, arrangement)`; the same roll, queue,
  timer and release paths (only `IStepBackBody` changes: a `BehaviourStepBackBody`).

## Steps 6-9 — the Athletics READ API (from steps 5, 5c)

Static, allocation-free, main thread (call from a view's `OnMissionScreenTick`), in
`Missions/AthleticsLogic.Api.cs`. Every answer is a snapshot struct from Core - never hold our
records.

```csharp
if (AthleticsLogic.TryGetReading(agent, out AthleticsReading r))   // false: a horse (pass RiderAgent),
{                                                                  // untracked, or no mission running
    r.Points; r.Pool; r.Fraction;   // points left (E), his FULL pool (the bar's length), 0..1 (bar fill)
    r.UsablePool; r.UsableFraction; // the health cap: grey the bar from UsableFraction to 1
    r.PeakShare;                    // f = min(E / (peak% × pool), 1): THE colour input (1 = green;
                                    // below the line blue; ≤ BarYellowBelowPercent% of the line yellow…)
    r.PeakFraction; r.InPeakZone;   // where to draw the peak marker (0.75); f ≥ 1
    r.Exhausted;                    // E = 0
    r.SpeedMultiplier; r.RunSpeedMultiplier; r.MountSpeedMultiplier; // applied now (1 = none)
    r.AthleticsSkill; r.IsHero; r.IsLeader;
    r.Enabled;                      // ModEnabled && AthleticsEnabled - off: reads full; the HUD must hide
}
AthleticsLogic.TryGetPeakShare(agent, out double f); // just f (false: not tracked)
if (AthleticsLogic.TryGetFormationStats(formation, out FormationAthleticsStats s)) // player team only
{
    s.Count; s.Exhausted; s.InPeak;  // men, empty, at full strength
    s.MeanPoints; s.StdPoints;       // "72 ± 8" (s.Describe() = "72 ± 8 (40 men, 2 exhausted)")
    s.MeanFraction; s.StdFraction;   // bar fill (shares - pools differ per man since 5c)
    s.MeanPeakShare;                 // the men's average f - colour the squad bar by it
    s.MeanHealth;                    // their average health left, 0..1 (step 9)
    s.LowFraction(k); s.HighFraction(k); // the ± band, k = FormationSpreadStdDevs, clamped 0..1
}
AthleticsLogic.FormationStatsVersion  // bumps every FormationStatsRefreshSeconds - redraw on change
AthleticsLogic.IsRunning
```
- Formation stats are recomputed by the logic's own tick (one O(N) pass over tracked fighters of
  `Mission.PlayerTeam`, bucketed by `(int)agent.Formation.FormationIndex`), so a view never loops
  agents itself. Enemy formations are not computed (ask if a step needs them). Since step 9 the
  PLAYER is left out (the orders menu's cards count the men under his command) and health is in.
- Since 5c pools differ per fighter: MeanPoints is an average of different-sized bars (the
  "72 ± 8" text); bars should FILL by MeanFraction and COLOUR by MeanPeakShare (f), the player's
  and target's bars by `r.Fraction` / `r.PeakShare`, with the peak marker at `r.PeakFraction`.
- **Master switch first (step 5b, CLAUDE.md)**: every view / feature checks
  `TraxSettings.Shared.ModEnabled` BEFORE anything else, live, and hides / undoes what it shows
  when it is off (5d: no step-back starts, a running one is released to the formation at once;
  6-9: every bar and panel hidden); add that "off" behaviour to the smoke's
  `MasterSwitchIsVanillaLive` step.
- **New parameters** (5d's `StepBack*`, 6's bar colours, 8's `ShowFormationHealth`):
  DESIGN Parameters row (initial value) + schema entry (NO default) + TraxSettings property +
  `"Key": value` in defaults.json + `dotnet run --project tools/DefaultsTool -- refresh`; move the
  row out of DESIGN's "Planned parameters". Retiring one = remove it from all of those (done that
  way in 5c for four keys); the tests name whatever is left. `AthleticsRules` takes `modEnabled`
  as an optional last ctor argument (`From` passes it); `DamageRules` takes `modEnabled`, then
  `upsideFollowsAthletics`.
- The HUD views are attached by `AthleticsLogic`'s first tick (`AthleticsLogic.Hud.cs`,
  `AttachHud`) - built in step 6, see below.

## Step 6 — Player bar (DONE 2026-09-27)

**What shipped.** `PlayerAthleticsView` (Hud/) - bottom right under the vanilla health bar: the
word *Athletics*, `current / pool`, a slim bar (fill coloured by f, white peak marker, the wounded
part dark red-brown, the rest dark grey); empty → *Exhausted*, word / number / frame red.
Prefab `module\GUI\Prefabs\TraxPlayerAthleticsBar.xml`, ViewModel `PlayerAthleticsVM`. 7 new
settings: `Bar{Yellow,Orange,Red}BelowPercent` (group "Bars - you and your target"),
`PlayerBar{Width,Height,OffsetRight,OffsetBottom}` (Advanced; UI pixels of the 1080p layout,
defaults 205 / 12 / 62 / 54 = under the hero bar's fill). 52 settings.

**The HUD plumbing - how steps 7-9 add a view.**
- `TraxHudView : MissionView` (Hud/TraxHudView.cs) owns ONE `GauntletLayer` + ONE movie + ONE
  ViewModel. Every frame (`OnMissionScreenTick` → `ReadFrame` → `Tick(in HudFrame)`) it asks
  Core's `HudGate.Decide` - ModEnabled FIRST, AthleticsEnabled, the view's Show… setting, Hide
  battle UI, photo mode, a fight mode (Battle/Duel/Tournament/Stealth), the player on the field
  (`NeedsPlayer`), the view's own `ViewConditionMet` - and the layer exists exactly while the
  answer is `HudHide.None`. Build / removal / "not shown at start" are logged with the reason
  (`TraxLog.Limited`, bucket `hud-layer`); `HudStats` (Core) counts time on screen, hidden time by
  reason, builds, removals by reason, refreshes, errors, and for bars the colours / empty / wound.
- While up: `Refresh(in frame, first)` every `HudRefreshSeconds` (read live; the first push on a
  new layer is flagged so the view can log it). Paused frames are skipped (vanilla does the same).
  `SuspendView`/`ResumeView` suspend the layer. `OnMissionScreenFinalize` / `OnRemoveBehavior` →
  `Finish` (layer removed, "mission end").
- FAIL SAFE: every entry point wrapped; an exception → `AthleticsLogic.Failed("hud.<site>")`
  ([error] + stack, first per site per mission) → `Disable`: layer removed, VM finalized, the view
  dead for the mission, one `[hud] … DISABLED for the rest of this battle` line. A movie that does
  not load (`IHudLayer.Create` false) is the same with the reason.
- The engine side is behind `IHudLayer` (Hud/HudLayer.cs) - `GauntletHudLayer` for real, the
  smoke's `FakeHudLayer` offline (the 5d IStepBackBody idea). `HudFrame` = what the view reads
  from the game per frame (a struct; the smoke makes them up).
- **A new view (step 7 example)**: `public sealed class TargetAthleticsView : TraxHudView` with
  `base("target bar", "TraxTargetAthleticsBar", SettingsSchema.ShowTargetBar)`; implement
  `CreateDataSource(in HudFrame)` (new VM, sized from settings) and `Refresh(in HudFrame, bool
  first)` (read, push, log what is new; return false while nothing to show); override
  `ViewConditionMet` ("someone targeted or lingering") + `ViewConditionText` ("nobody targeted");
  `OnVisibleFrame(dt)` for per-frame bar stats (`Stats.AddBarTime`); `OnLayerGone` to drop the
  VM. Prefab `module\GUI\Prefabs\TraxTargetAthleticsBar.xml` (file name = movie name, keep the
  `Trax` prefix - names are global across modules). One line in `AthleticsLogic.AttachHud`. The
  [summary] lines come for free (`WriteHudSummary` walks every attached view). Smoke: copy
  `HudPrefabIsValid` for the new prefab (make the VM type a parameter) and drive the view with
  `Frame(...)` + `FakeHudLayer`.
- Bar maths / colours for 7 (the target bar reuses them): Core `BarMath.Band`, `Fill`, `Usable`,
  `PeakLine`, `DisplayNumbers`, `ColorHex`; `PlayerAthleticsVM` is a template (copy, don't share -
  each prefab binds its own VM's names).

**Gauntlet binding findings (verified in source; RESEARCH §G "Step 6 addendum").**
- Only STRINGS are converted by the binding (to Sprite / Brush / int / Color); everything else is
  handed to the widget setter by reflection → a VM property's type must EQUAL the widget
  property's: float for SuggestedWidth / Margin* / *Offset / InitialAmountAsFloat, `Color` for
  Color and Brush.FontColor, bool for IsVisible, string for Text. A mismatch throws inside the
  game's binding. The smoke checks every @binding's type.
- Unknown attribute names are silently IGNORED by the game - a typo shows nothing and logs
  nothing. The smoke resolves every attribute against the real widget types.
- `FillBarWidget` draws a SHARE (Initial / Max of its own width) - use it for every fill instead
  of binding pixel widths; the UI scale then applies by itself. A marker at a share = a
  FillBarWidget whose (invisible) fill has a right-aligned child.
- `Brush="…"` before `Brush.FontSize` / `Brush.FontColor` (BrushWidget clones its brush on first get).
- Release the movie before `RemoveLayer`; never `AddLayer` a movie that did not load (releasing it
  throws). `UIResourceManager.WidgetFactory.IsCustomType(movie)` = the prefab is installed.
- Views added mid-mission get `OnMissionScreenInitialize` but NOT OnBehaviorInitialize / AfterStart
  / EarlyStart - do setup lazily.

**Gotchas met.**
- OfflineSmoke: `Program`'s own static fields initialise before `Main` registers the assembly
  resolver - a static field typed from a game DLL (a `Color`) kills the smoke at start. Keep such
  statics in a nested class.
- The smoke needs `TaleWorlds.DotNet` + `TaleWorlds.MountAndBlade.View` references once it
  touches `HudFrame` / a `MissionView`.
- The Module now also references `TaleWorlds.GauntletUI.PrefabSystem` (WidgetFactory).
- `HudFrame` is public (it appears in protected members of the public view classes).

**UNVERIFIED (PLAYTEST §7) + settling lines.**
- The bar draws as the prefab says (RESEARCH #12): `[hud] player bar: layer created … movie
  TraxPlayerAthleticsBar loaded OK (13 widgets)` + `first values pushed …` prove the data side; the
  eye proves the drawing.
- Placement at other resolutions / UI scales, riding (RESEARCH #8): the four `PlayerBar*` settings
  move it live; Anton reports the numbers that look right → defaults.json.
- War Sails steering (RESEARCH #13): the vanilla block drops 60 px (Passive state) - ours does not
  follow; if it collides, bind `OffsetBottom` to a second value while `IsAgentStatusPrioritized`
  is false (read it from the vanilla `MissionAgentStatusUIHandler` view's data source).
- Colours by f: the `[hud] player bar: <COLOUR> for the first time this battle at … - f …` lines;
  the summary's `colours on screen` line.
- Hide/show reasons: every `[hud] player bar: layer removed at … - <reason>`; the summary's
  `removed Nx (…)`, `hidden: …`.

## Step 5e — Attack rate: the whole cycle follows m (research 2026-09-27)

Anton (DESIGN §2, "Attack speed"): the RATE of attacking, not the swing animation - at m 0.5 a
fighter who attacked once a second attacks once every two seconds. m = S + (1 − S) × f is step
5c's attack multiplier (`TrackedAgent.SpeedMultiplier`), already applied by the stat decorator.

**The attack cycle on action channel 1** (v1.4.8 `Modules\Native\ModuleData\action_types.xml` +
`Agent.ActionCodeType`)
- Melee: `ReadyMelee` (19, stage AttackReady - the wind-up, then the blow held ready) →
  `ReleaseMelee` (20, AttackRelease - the swing AND its follow-through; a hit on flesh stays in it)
  → [`BlockedMelee` (22): `act_blocked_*` / `act_quick_blocked_*`, the attacker's recoil when his
  blow is blocked or parried; `ParriedMelee` (21) is only the couched-lance / braced-spear
  "parried" actions] → anything else (idle, guard, defend, flinch) = the PAUSE → the next ready. A
  chained blow goes Release → Ready directly (`act_ready_continue_*`; native
  `percentage_to_quick_ready_chance_for_continued_action` 0.85).
- Ranged: `ReadyRanged` (15 - the draw, then the hold / aim) → `ReleaseRanged` (16) /
  `ReleaseThrowing` (17) → `Reload` (18: bow `act_reload_bow_*` = nocking the next arrow,
  crossbow `act_reload_crossbow*` mid + last phase, javelins / stones taking the next one) → pause
  → the next ready.

**Which property drives which phase** (the engine reads driven properties natively - its formula
is not visible; this is what the managed code and the data show)
- `SwingSpeedMultiplier` (66) / `ThrustOrRangedReadySpeedMultiplier` (67): attack animation
  speed of swings / of thrusts, bow draws and throws. Sandbox: 0.93 + the weapon-skill effect +
  perks (SwiftStrike …), assigned with `=` every recompute. Natively combined with the weapon's
  own speed and the global `ready_speed_multiplier` 2.2 / `release_speed_multiplier` 1.2
  (native_parameters.xml) → they scale the READY (wind-up) and the RELEASE (swing incl. its
  follow-through). Recovery after a flesh hit is still the release (the native on-hit slow-down,
  `on_weapon_hit_slow_down_factor_swing` 0.6 then `…_speed_regain_acceleration` 4.8, acts on top).
- `ReloadSpeed` (69): reload (the CrossbowReloadSpeed skill effect; set for every weapon, 0.93
  base) → the Reload phase. For bow nocking UNVERIFIED - the phase lines measure it.
- `BipedalRangedReadySpeedMultiplier` (95, 0.6) / `BipedalRangedReloadSpeedMultiplier` (96,
  0.95): global managed parameters copied onto every agent, an extra on-foot factor - left alone
  (scaling them too could square the penalty; RESEARCH §C, unchanged).
- `HandlingMultiplier` (68): weapon HANDLING (perks Athletics.Fury "weapon handling while on
  foot", WrappedHandles, StrongGrip, Counterweight) - how fast the weapon moves between stances,
  the DEFENCE side (native `defend_speed_multiplier` 3.2). RBM's stamina scales it (its tired men
  block slower); we do NOT - Anton: blocking is never slowed. `OffhandWeaponDefendSpeedMultiplier`
  (97, shields) likewise untouched.
- The block / parry recoil (`BlockedMelee`): its speed is native (`added_animation_duration_for_
  blocked_attacks` 0.095 s is added to it); no driven property is visibly tied to it → probably
  NOT scaled. The phase lines measure it ("recoil after a block").
- No driven property sets the time BETWEEN attacks: that is the AI's decision (below) - or, for
  the player, his fingers.

**Per-agent action speed - NOT safe, not built**
- `Agent.SetCurrentActionSpeed(channel, speed)` → native `IMBAgent.SetCurrentActionSpeed`: an
  ABSOLUTE playback speed for the current action, and there is no getter (IMBAgent exposes the
  current action's type, stage, direction, priority, progress and weight - not its speed). We
  could only OVERWRITE what the engine chose (weapon speed × skill × perks × ready/release
  multipliers), never multiply it.
- Vanilla calls it only on usage animations the managed code started itself
  (`SiegeWeaponMovementComponent` every tick, `Ballista` reload) - never on a combat action.
  Combat actions are started and paced by the native combat system, which rewrites a release's
  speed every frame after an impact (the slow-down / regain above) - an override would be
  overwritten or fight it. `SetActionChannel`'s speed argument only exists for actions WE start.
- `SetCurrentActionProgress` (winding the progress back every tick) would drag the weapon's
  collision sweep backwards - hit detection at risk.
- The next lever IF the playtest shows the block recoil at fresh speed for tired men: read the
  recoil's native speed from its progress rate, then `SetCurrentActionSpeed(1, m × that)` on the
  rising edge into BlockedMelee only - an experiment behind a switch.

**The AI's pause** - `AgentStatCalculateModel.SetAiRelatedProperties`, called by every stat
model's UpdateHumanStats (Sandbox ~1107, CustomBattle ~345), assigns every Ai* value with `=` on
every recompute (so our scaling after the base never compounds). `num` = AI level from the melee
skill (× the tournament multiplier), `num2` = from the wielded weapon's skill.
- `AIAttackOnDecideChance` (36) = clamp(0.1 × (0.16 easy | 0.48) × (3 − Defensiveness), 0.05, 1)
  = 0.05-0.144: the chance to ATTACK at a combat decision. Vanilla lowers it itself for defensive
  orders (Formation → `Agent.Defensiveness` setter → UpdateAgentProperties;
  `Formation.IsDefenseRelatedAIDrivenComponent` groups it with the defence values) - the engine's
  own "attack less often" knob. → × m (no 0.05 floor: m 0.2 must mean five times rarer).
- `AIAttackOnParryChance` (8) = 0.08 − 0.02 × Defensiveness: the chance to strike back right after
  a parry - a riposte is an attack. → × m.
- `AiShootFreq` (3) = 0.3 + 0.7 × num2: how readily the AI looses. → × m.
- `AiWaitBeforeShootFactor` (4) = 1 − 0.5 × num2 (0 for units in a formation with an
  AmmoSupplyLogic - siege defenders: `Formation.AddUnit` → `ResetAiWaitBeforeShootFactor`): the
  aim before the shot. → ÷ m (0 stays 0).
- Left alone: `AIDecideOnAttackChance` (10) = 0.5 × Defensiveness (rises with defensiveness - the
  chance to react to the ENEMY's attack: defence); `AIHoldingReadyMaxDuration` (50) /
  `…VariationPercentage` (51) (how long a readied blow may be held, 0.25 → 0 s with the AI level;
  RBM sets 1 s) - a longer hold keeps the weapon up and the guard down = worse defence, and the
  pause belongs BEFORE the ready, guard up; `AiAttackCalculationMaxTimeFactor`,
  `AiDecideOnAttack*`, `AISetNoAttackTimerAfterBeing*Ability`, `AiAttackOnParryTiming`, `AiKick`,
  `AiTryChamberAttackOnDecide` (signed timing offsets and "ability" values, higher = a smarter AI -
  scaling them changes the AI's skill, not its rhythm); every defence value.
- Their exact native use is UNVERIFIED → the phase lines measure the AI's pause and aim by f.

**A hold with the guard up - NoAttack**
- `Agent.AIScriptedFrameFlags.NoAttack` (2) set with `SetScriptedFlags(GetScriptedFlags() |
  NoAttack)` WITHOUT a scripted position is vanilla's own "do not attack" (`Agent.UseGameObject`
  does exactly this for objects that lock the user's frame). Step 5d: NoAttack is an extra flag;
  the combat AI keeps its target and (5d UNVERIFIED #4) its guard.
- So a tired AI fighter can be held from attacking for a measured time after his swing, guard up
  - the one lever that sets the pause exactly, however the native AI uses the chances above.
- The flag word is SHARED (step back, item pickup, siege objects): set NoAttack only on a man with
  neither GoToPosition nor NoAttack already; clear it only while no scripted frame, game object or
  ladder queue is on him - otherwise wait until there is none (never cancel the game's job, never
  leave ours behind). `IsUsingGameObject` / `IsInLadderQueue` / `IsMainAgent` are managed;
  `IsAIControlled` and the flags are native (a seam for the smoke, like 5d's IStepBackBody).

**The player** has no AI: his cycle is the ready (he cannot release before
`min_ready_anim_weight_for_quick_attack` 0.95 of the wind-up) + the release (+ a block recoil) +
his own pause. The first two follow the animation properties - hammering the attack button while
tired gives the slowed rate IF the engine honours them (5c UNVERIFIED #2). The "you" lines settle
it; his pause is his.

**Choice**
1. T1 (steps 5 / 5c, unchanged): swing, thrust/draw, reload × m - wind-up, swing, reload.
2. T2 `AttackRateAiDecisions` (on, A/B switch): attack and riposte chances × m, shooting chance
   × m, the wait before a shot ÷ m - in the same decorator pass, re-applied by the same
   recomputes; switching it re-applies to every tired fighter (budgeted).
3. T3 `AttackRatePaceHold` (on, A/B switch): after each MELEE swing of a tired AI fighter on foot,
   NoAttack until his next release can come no sooner than his fresh cycle ÷ m after this one
   (fresh cycle = his own release-to-release while m was 1, else the mission's AI average). Melee
   only - a hold across a reload is another animal; ranged gets T1 + T2 and the lines tell whether
   that is enough.
4. Not built: per-agent action speed, AIHoldingReady (defence), Bipedal* (squaring).
5. Measured, per f band, melee / ranged × AI / you: every phase, the cycle, m, the target (the
   band's fresh cycle ÷ m) and measured ÷ target with a verdict word; the pace holds; the guard
   by f (tired men must not block less). These supersede step 5c's "attack speed check" lines.

**Built (DONE 2026-09-27)** - file map in CLAUDE.md "Layout"
- Settings (54, group "Tired fighters", after the attack speed): `AttackRateAiDecisions` true,
  `AttackRatePaceHold` true (A/B switches; schema, TraxSettings, defaults.json + refresh, DESIGN).
- Core `AttackRate.cs`: `AttackKind`, `AttackPhase` (wind-up, held, release, clean release, recoil,
  reload, pause), the hold's reasons (`PaceNotHeld`, `PaceRefusal`, `PaceEnd`, `PaceRelease`),
  `AttackRateRules` (live; the master switch first), `AttackRateMath` (ScaleChance / ScaleWait,
  CycleCap, TargetCycle, FreshReference, ExpectedReady, HoldUntil, HoldNeeded, Verdict + the
  plumbing constants). `AttackRateStats.cs`: per group (melee / ranged × AI / you) × f band - every
  phase's MeanStd, the cycle, m, 1/m (target = the peak's cycle × the band's mean 1/m), left-out
  counts (mixed bands, beyond the cap, cancelled readies, chained, a step back inside), the holds,
  the guard by f, the T2 recompute count, the [summary] lines. 13 tests (274; step 5c's 3
  interval tests went with their classes).
- Superseded and removed: `BinnedIntervals`, `SpeedVerdict`, `IntervalStats` (SpreadStats.cs), the
  three "attack speed check" summary lines, `TrackedAgent.ReleaseBin/ReleaseAsked/ReleaseMixed`.
- Module: `SpeedPenalty.ScaleAiDecisions` + `AiSnapshot`; the decorator applies it after `Scale`
  while the switch is on (read live, counted); `AthleticsLogic.AttackRate.cs` (phases on every
  action change, the ready-progress poll, cycles + fresh references, the hold: asked, ticked,
  lifted, the guard, the switch notes, the logs, the summary); `PaceBody.cs` (`PaceState`,
  `IPaceBody`, `GamePaceBody`); hooks: ObserveAction (phases first), StartRelease (cycle; a swing
  while held), EndRelease (after the step-back roll: the hold), OnAgentShootMissile (ranged cycle),
  the tick (ready poll; `TickPace` after `TickStepBacks`), OnMeleeHit (guard), Untrack,
  ApplySettingsChange (phases reset on an on/off; T2 re-applied), ApplyFighterSpeed (first slowed),
  WriteAthleticsSummary (`ClosePace` first, the lines after the Athletics block), the step back's
  start (`SteppedBackThisCycle`).
- Smoke (39 steps): the AI-decision lever touches exactly its four values; the decorator (T2
  applied, off live, never on horses, never a defence value); `Program.AttackRate.cs` - A: the
  animations alone read "too fast" (about 75% when empty); B: with the hold the empty band is 100%
  on target; every hold path with `FakePaceBody` (time up, a swing slipping through, a game job
  waited out, a long plain frame lifted after 3 s, mounted, left the field, switched off, mission
  end), refusals, riders, chained blows,
  no reference, the T2 switch re-applied, the summary; the first-slowed line through the real
  decorator; the master switch lifts a running hold; the step-back smoke's cycles are left out.
  deploy.ps1 green (guard OK, smoke OK, installed).

**Decisions**
- **Phases are filed at the band at their START, with the m in effect then** (before this
  action's own charge - `PhasesOnAction` runs first in `ObserveAction`). A cycle keeps step 5c's
  rule: the band after the first release's charge, the same at the second (else "mixed").
- **Wind-up vs held**: the ready's progress is polled (one native call) only for a fighter in a
  ready that has not reached `ReadyFullProgress` 0.98 - the poll stops at full wind-up; a ready
  released before that is all wind-up (held 0). A ready that ends in no attack is counted apart.
- **The pause** = from the end of an attack (its release, recoil or reload) to the next ready; a
  ready straight out of the attack is a "chained" pause 0; a pause spanning a step back is left
  out (that pause is the step back's), and so is the cycle (`SteppedBackThisCycle`, set when a step
  back STARTS, reset at each release).
- **The cap** (`MeleeCycleCapSeconds` 4 s, `RangedCycleCapSeconds` 12 s, ÷ m): longer cycles,
  phases and pauses are not a fighting rhythm - left out, counted. Log / reference plumbing like
  5c's 30 s cap; not a gameplay number.
- **The hold's arithmetic**: next ready may begin at `release start + fresh ÷ m − expected ready`,
  expected ready = his last melee ready × its m ÷ m now. So his next RELEASE lands at release
  start + fresh ÷ m, the target, if the AI readies the moment it may. Holds under
  `MinHoldSeconds` 0.1 s are "not needed" (the swing and ready already fill it - this is what
  T1 + T2 look like when they work).
- **Fresh reference**: his own release-to-release cycles while m was 1 at both ends, on foot, AI,
  within the cap; else the mission's AI mean once it has 5; else no hold ("no fresh cycle known
  yet"). Per fighter first: a dagger and a two-hander keep their own rhythms.
- **Order at a swing's end**: the step-back roll first; a step back asked for takes precedence
  (it holds attacks itself) - but only if the tick STARTS it: the hold is queued too, and
  `TickPace` (after `TickStepBacks`) drops it for a man now stepping back; a REFUSED step back
  leaves the hold to run (review 10a R1). A swing ending straight into a ready (a chain) is not held. The
  tick refuses a hold whose man has readied or swung since ("too late"), so NoAttack never lands
  on a readied blow.
- **Queue, then the tick** (5d's rule): the hold is asked for at the swing's end (maybe inside a
  hit callback) and started by `TickPace` in the same or the next tick; a swing while held is
  flagged there and lifted by the tick.
- **Flag hygiene**: `GamePaceBody.Start` refuses a man with GoToPosition, NoAttack, a game object,
  a ladder queue, a detachment or a horse; `Release` lifts OUR NoAttack only while no object, ladder
  queue or walk to an object is on him (their NoAttack may be their own) - else it waits
  (re-checked every 0.25 s) and clears it once he is free (a vanilla job that set NoAttack itself
  is over by then - clearing a leftover is harmless). Under a PLAIN scripted frame it waits at most
  `WaitingMaxSecondsUnderAFrame` 3 s, then lifts it anyway (counted "under a long scripted frame"):
  whether the native keeps our flag through a new frame is unseen, and such jobs (strategic areas,
  duel set-ups, swimming) may want the man to fight; the step back's 1.5 s frame ends before
  that. A detachment alone is not waited on (it owns no NoAttack). Left the field = no engine call;
  mission end = lifted through the engine before the summary.
- **T2 values**: no 0.05 floor on the attack chance (vanilla clamps its own formula there) - m
  0.2 must mean five times rarer. The riposte chance is an attack decision → × m.
  `AiWaitBeforeShootFactor` 0 (siege defenders) stays 0.
- **Not scaled on purpose**: `AIHoldingReadyMaxDuration` (a longer hold = a raised weapon and a
  lowered guard), `AIDecideOnAttackChance` (defence), handling, shield defend speed, Bipedal*.
- **First slowed**: the first `ApplyFighterSpeed` of the mission with m below 1 snapshots every
  touched value before and after `UpdateAgentProperties` and checks each against its factor.
- **No new gameplay numbers** besides the two switches: 0.98 progress, 0.1 s, 4 s / 12 s, 5
  samples, 100 starts a tick, 0.25 s, 3 s under a frame, ±15% are plumbing / log constants
  (documented here).

**Gotchas**
- Python / sed on markdown is against the house rule (CLAUDE.md) - PLAYTEST was edited by a
  UTF-8-safe script once in this step and checked clean (no mojibake); use Edit/Write.
- The smoke's offline `Mission` is null: `SafeNow()` reads 0, so "switched … at 0.0 s" lines are
  offline artefacts.
- The swing's own speed in the smoke is the m before its charge (the engine starts it at the old
  properties) - the first cycle of a new band carries the previous swing: the animations-alone
  empty band reads ~74%, not exactly 4.9 / 6.5.

**UNVERIFIED - only the game can tell (PLAYTEST 3n; the line that settles each)**
1. The engine honours the animation multipliers through the whole ready and release, player and
   AI (5c #2) → the `attack rate, … <band>` rows: `wind-up`, `swing`, `draw`, `reload` ≈ (x1/m).
2. The native AI follows the four AI values (a longer pause, a longer aim) → with
   `AttackRatePaceHold` OFF (battle 2): the AI melee `pause` rows (x…) > 1 and the verdict; ranged
   `aim` / `pause` rows.
3. NoAttack with no scripted position holds swings in open melee → `pace hold ends: … a swing
   started anyway` near 0, and the AI melee verdict ON TARGET with the hold on.
4. Held men keep their guard → `guard by f … while held by the pace hold` not below the peak row.
5. The block recoil is not scaled → `recoil after a block … (x1.00)` in the tired rows (the known
   gap; the next lever is in the research above).
6. A ready's progress reaches 0.98 at full wind-up → `held` above 0 in the AI rows (0 everywhere =
   the poll never saw full: wind-up then includes the hold).
7. The fresh reference is sane → `first pace hold … fresh cycle 1.xx s (his own, N samples)` and
   `not held … no fresh cycle known yet` small.
8. The AI readies the moment the hold ends → `the next ready came avg … after a hold ended` near 0.
9. Cost: the ready poll in a 1000-man battle → `Athletics tick cost` (compare with a 5c/5d log).

## Step 7 — Looked-at NPC bar (LATER - Anton, 2026-09-27)

- **Its settings are not in the schema** (step 10b, R21): `ShowTargetBar`, `TargetBarMaxDistance`,
  `TargetBarLingerSeconds` wait in DESIGN's "Planned parameters". Building the step = move the
  three rows up, add them to SettingsSchema (group "Your Athletics bar"), TraxSettings and
  defaults.json, run `DefaultsTool refresh` (see "Step 10b" below).
- `TargetAthleticsView : TraxHudView` - the recipe above (Step 6, "A new view"). Own raycast
  `Mission.RayCastForClosestAgent` from `MissionScreen.CombatCamera` every `HudRefreshSeconds`
  (inside `ViewConditionMet` or `Refresh` - the frame gives `Player`; read the camera via the
  view's `MissionScreen`), mount → `RiderAgent`, linger `TargetBarLingerSeconds`, range
  `TargetBarMaxDistance`. RBM-style place: top centre. Colours, numbers,
  wounded part, marker: `BarMath` exactly as the player bar.
- Read: `AthleticsLogic.TryGetReading(target.IsMount ? target.RiderAgent : target, out var r)`.
- Hidden while `ModEnabled` is off - the base does it (HudGate); the smoke's master-switch step
  already covers the player bar - add the target view there too.

## Step 8 — Squad bars above formations (LATER - Anton, 2026-09-27; BUILT as step 20 in its "hold ALT" form - see "Step 20")

- **Its settings are not in the schema** (step 10b, R21): `ShowFormationBars`,
  `FormationBarsAlways`, `FormationBarHeight` wait in DESIGN's "Planned parameters" (bring them
  back as for step 7; a group of their own, e.g. "Squad bars", before Advanced).
  `ShowFormationSpread`, `FormationSpreadStdDevs`, `ShowFormationHealth` are live already (the
  orders strip) - reword their hints to name both places.
- `FormationAthleticsView : TraxHudView` (`NeedsPlayer` - decide: can the player command while
  down? probably keep true). Player formations = `PlayerTeam.FormationsIncludingEmpty`,
  `CountOfUnits > 0`, `PlayerOrderController.IsFormationSelectable`. Mean/std: already computed
  by step 5 - `AthleticsLogic.TryGetFormationStats(formation, out var f)`, redraw when
  `FormationStatsVersion` moves (band = `f.LowFraction(FormationSpreadStdDevs)` ..
  `f.HighFraction(…)`). Average HEALTH is in the stats since step 9 (`f.MeanHealth`; the strip's
  `OrderStripMath.Numbers` / `Band` / `HealthText` and `OrderStripCellVM` are reusable). Position = `MBWindowManager.WorldToScreen(CombatCamera,
  CachedMedianPosition.GetGroundVec3() + (0,0,FormationBarHeight))`, hide when w < 0.
  Anchor: bind `ScaledPositionXOffset`/`YOffset` (floats - vanilla's GamepadCursor.xml does it;
  step 6 finding) before reaching for an own `Widget` subclass (§G, UNVERIFIED #7). A list VM
  (`MBBindingList`) + `ItemTemplate` for one item per formation. `FormationBarsAlways` off →
  `ViewConditionMet` = markers shown (key 5 held or `Mission.IsOrderMenuOpen`).
- Colour squad bars by `f.MeanPeakShare` with `BarMath.Band`; fill `MeanFraction` (a FillBarWidget).

## Step 9 — Orders-menu strip (research 2026-09-27, verified in the v1.4.8 source + RTS Camera 5.3.38)

Anton: "leave the strip under the orders cards - it will help me have at least some vision of the
state of the troops there" / "below the arrows remaining, the Athletics state and the average
health for the squad". DESIGN §3 item 4 + Additions.

**Research - where the vanilla cards are.** (decompiled beside the others: the generated OrderBar
movie → `..\reference\game-decompiled\GauntletUI.AutoGenerated0-OrderBar\`; RTS Camera Command
System's two order patches → `..\reference\RTSCamera.CommandSystem-5.3.38\`.)
- View `MissionGauntletSingleplayerOrderUIHandler : GauntletOrderUIHandler` (naval:
  `MissionGauntletNavalOrderUIHandler`, same base, movies `NavalOrderBar`/`NavalOrderRadial`) builds
  ONE layer `new GauntletLayer("MissionOrder", 14)` in `OnMissionScreenInitialize` and loads movie
  `OrderBar` (keyboard) or `OrderRadial` (`BannerlordConfig.OrderType`) over `MissionOrderVM`. The
  handler's `_gauntletLayer` / `_dataSource` are protected - but NO reflection is needed:
  `MissionScreen.FindLayer<GauntletLayer>("MissionOrder")` (public, ScreenBase) → `layer.UIContext.Root`
  (public) is the live widget tree, and `UIContext.EventManager.PageSize` the screen in pixels.
- The movie (Modules\Native\GUI\Prefabs\Order\Bar\OrderBar.xml; Radial identical for the cards) holds
  the cards TWICE: layout A (`IsHidden="@UseAlternativeFormationLayout"`) = two columns, left
  `TroopItem0..3`, right `TroopItem4..7`, each a vertical `ListPanel` at the screen edge (margins 20,
  vertically centred); layout B (`IsVisible="@UseAlternativeFormationLayout"`) = one row at the top
  centre, `TroopItem0..7`, hidden cards collapse. `UseAlternativeFormationLayout = Input.IsGamepadActive`
  (MissionOrderVM.Update) - keyboard players always see A.
- `MissionOrderTroopControllerVM.RefreshTroopItemBindings`: `TroopItem k` = the TroopList item whose
  `FormationIndex == k` (else an empty VM with `IsValid false`). TroopList = the player team's
  formations with men (`CountOfUnits > 0`, not only the player) - added on open (`UpdateTroops`), on
  `AddTroops` (agent build) and every 2 s; NEVER removed mid-battle (an emptied formation keeps its
  card, count 0). So **slot k of a layout = FormationClass k** (Infantry 0, Ranged 1, Cavalry 2,
  HorseArcher 3, Skirmisher 4, HeavyInfantry 5, LightCavalry 6, HeavyCavalry 7).
- Card prefab OrderTroopItem.xml: the SLOT is a 131 × 223 widget (MarginTop 30, MarginBottom 10 - "must
  fill space even when invisible") holding TWO `OrderTroopItemBrushWidget`s: the highlight (no
  children) and the card (`IsVisible="@IsValid"`, children, `CurrentMemberCount="@CurrentMemberCount"`
  - a real public int property of the widget; `FormationClass=` is silently ignored, no such
  property). Inside the card at the bottom: the ammo `FillBarWidget` 77 × 3, MarginBottom 14 (Anton's
  "arrows remaining"); below it the "Orders" row: the current-order icon + the target formation icon,
  30 × 30 each, centred, from 10 px ABOVE the card's bottom to **20 px below it** (PositionYOffset 30
  in a 50-high panel), vanilla only while `HasTarget` (a targeted order + `Formation.TargetFormation` -
  common in battle).
- The card VM's `CurrentMemberCount` = `CountOfUnits` minus the player when he is in it, updated AT ONCE
  by `Formation.OnUnitCountChanged` and pushed to the widget synchronously - an exact cross-check of
  "slot k is formation k".
- The generated code (TaleWorlds.MountAndBlade.GauntletUI.AutoGenerated0/1.dll) builds REAL widgets
  of the real types in the prefab's order (`_widget_1_0_0_0_0..3` left column, `_1_0_0_1_0..3` right,
  `_1_0_1_0_0..7` the row; each slot a generated `…OrderTroopItem__DependendPrefab : Widget`). So the
  tree can be walked and read like any other; nothing is patched or injected.
- Geometry: children lay out in parent-local pixels (DefaultLayout: `child.Layout(0, h, w, 0)`), so
  `Widget.GlobalPosition` (sum of LocalPositions) and `Widget.Size` are true screen pixels, updated only
  while visible (a hidden widget keeps stale values - a first open may read size 0 for a frame).
  `IsRecursivelyVisible()` walks the parents. Our layer's `ScaledPositionXOffset` /
  `ScaledPositionYOffset` / `ScaledSuggestedWidth` take PIXELS (they divide by the context's scale,
  which `UIContext.Initialize` sets before any movie loads) - so a cell lands on the card's pixel at any
  resolution and UI scale. Layers draw in order: vanilla's order layer (14) over ours (1).
- At 1920 × 1080 (16:9, UI scale 1) layout A's column is 4 × 263 = 1052 px in 1040 → the bottom card
  ends at y 1056: **24 px to the screen's edge** (more at 16:10 or a smaller UI scale). The gap between
  stacked cards is 40 px; vanilla's order icons take its first 20 px in the middle 60 px.
- Fonts: the HUD brush `AgentHUD.Interaction.Text` is FiraSansExtraCondensed-Regular - it has `±`
  (177) but **no `♥`** (9829; no game font has it) → the health reads `HP 81%`. Width at size 13:
  "72% ± 8" ≈ 34 px, "HP 81%" ≈ 35 px (xadvance × 13/32) - each fits in the 35 px beside the 60 px
  icon pair of a 131 px card.
- Transfer popup cards (OrderTransferTroopItem.xml) are `OrderTroopItemBrushWidget`s too, but ALONE
  under a ButtonWidget - a real slot holds two brush widgets. That is the card filter.
- Orders menu open = `Mission.IsOrderMenuOpen` (set with `IsToggleOrderShown`); can't open without
  `Agent.Main` (MissionOrderVM.CheckCanBeOpened), so the usual player gate costs nothing. While open,
  `BannerlordConfig.SlowDownOnOrder` runs the mission at 0.25 speed: mission-time timers (the HUD
  refresh, FormationStatsRefreshSeconds) run 4x slower in real time - the strip places its cells from
  the frame's real dt, every frame.
- **RTS Camera Command System 5.3.38** (Anton runs it): keeps the vanilla handler and the
  "MissionOrder" layer (Harmony on `TickInput` / `OnMissionScreenTick` / `OrderController_OnTroopOrderIssued`
  only - the TroopItem bindings are untouched) but OVERRIDES the prefabs: its OrderBar has ONE layout
  (8 cards), each column `VerticalBottomToTop` (TroopItem0 = Infantry at the BOTTOM left), the slot is
  a clickable `ButtonWidget`, and its card shows the current-order icon ALWAYS (only the target icon
  waits for HasTarget). Same card size and slot structure → the same reader works; the bottom-left
  card is then Infantry.

**Decision - alignment technique: read the live cards.** On each open the view walks the "MissionOrder"
layer's tree once (a few hundred widgets), keeps the card widgets in document order and groups them
in sets of 8 (vanilla 16 = 2 layouts, RTS Camera 8 = 1); every frame it reads the ≤ 8 cards of the
set that is visible: `GlobalPosition`, `Size`, `IsRecursivelyVisible`, `CurrentMemberCount` (no
allocation). Slot k ↔ FormationClass k, CONFIRMED every frame by the member count (card vs
`CountOfUnits` − the player, ±1). Exact at every resolution / UI scale / layout / RTS Camera's reversed
columns, and no layout maths copied from the prefab. Replicating the prefab maths was rejected: it
breaks on RTS Camera (reversed columns, one layout) and on any other order-UI mod.

**The cell** (UI px of the 1080p layout, all Advanced settings): the card's width; the NUMBERS row
at the card's bottom + `OrderStripTextOffset` (1): "72% ± 8" left, "HP 81%" right (the middle stays
free for vanilla's order icons); the BAR at + `OrderStripBarOffset` (20, under the icons), 
`OrderStripBarHeight` (4) thick, `OrderStripSideMargin` (2) in from the card's sides: the mean fill
coloured by the men's f (BarMath bands), a translucent ± band (FillBarWidget's ChangeWidget from low
to high - shares, pixel-free), a 1-px peak tick. 24 px deep = fits the 1080p bottom card exactly; a
cell that would still leave the screen is lifted to its edge (logged, counted).

**Fallback - the compact panel** (top centre, `OrderPanelOffsetTop` / `OrderPanelWidth`): one row per
formation with men ("1 Infantry  72% ± 8  HP 81%" + the same bar), in the cards' order. Used for the
rest of an open when: `OrderStripUnderCards` is off; no "MissionOrder" layer and no other layer with
cards; no cards / not whole sets of 8; two sets visible at once; no card visible or laid out 0.5 s
after the open; or a mismatch (a formation with men but no visible card, a visible card whose members
disagree with its formation by more than 1) lasting 1 s (engine plumbing constants, like 5d's). The
next open tries the cards again. Every fallback is logged with its reason and counted.

**Built (DONE 2026-09-27).**
- Core `OrderStrip.cs`: `OrderCard` / `OrderCardFrame` (≤ 32 cards, reused), `StripFormation`,
  `OrderStripMath.Match` (sets of 8, the drawn set, slot k vs formation k by members ±1 →
  Aligned / NotYet / Mismatch / Problem + `StripIssue` + `Describe()`), `PlaceCell` (pixels, lift at
  the bottom edge), `Signature`, `Numbers` / `AthleticsText` / `HealthText` / `Band`, `StripLayout`
  (cell = card bottom + min(text, bar offset); no negative margins), `OrderStripStats` (+ the
  [summary] line). Formation stats: `MeanHealth` (+ "health avg 81%" in the summary). 288 tests.
- Module `Hud/OrderCards.cs` (`IOrderCardSource` / `GauntletOrderCards`, `IStripFormations` /
  `MissionStripFormations`), `Hud/OrderStripView.cs`, `Hud/OrderStripVM.cs` (root + 8 fixed cell VMs
  in one `MBBindingList`, bound TWICE by the prefab: the cells and the panel rows),
  `module\GUI\Prefabs\TraxOrderStrip.xml`. Base `TraxHudView` got `OnLayerFrame(in HudFrame)` (every
  frame, before the refresh), `QuietConditionToggles` (the menu's open/close → verbose after the
  first build), `ViewConditionWhen`, `AddSummaryLines`; `HudFrame.OrderMenuOpen`;
  `HudStats.ConditionName` ("orders menu closed" in the summary).
- The squad stats now LEAVE THE PLAYER OUT (`a.IsMainAgent`) - so our count = the card's count - and
  read health live (`HealthOf`). The Athletics summary's "your formations" line follows.
- `ShowFormationSpread` / `FormationSpreadStdDevs` drive the strip's band and "± N" (N = k·σ in
  points of the bar); `ShowFormationHealth` the "HP"; 9 new settings (63).
- References: `TaleWorlds.MountAndBlade.GauntletUI.Widgets` (main bin - the card widget type) and the
  game's own `System.Numerics.Vectors` 4.1.3.0 (Widget.GlobalPosition / Size are Vector2; the .NET
  Framework reference assemblies do not carry that version).

**Gotchas met.**
- **A widget's OWN @bindings resolve against its own DataSource** (`GauntletView.ViewModelPath`
  appends it; `RefreshBinding` binds a LIST DataSource as a list only - its widget's properties are
  never set). `IsVisible="@StripShown"` on the `{Cells}` widget would have been silently ignored;
  the visibility sits on a widget around the list. The smoke's prefab walker now resolves own
  attributes against the DataSource and FAILS any @binding on a list widget (proved by breaking it
  on purpose).
- The shared `hud-layer` notice bucket (30, then 1/s) is spent late in a fast smoke run - the
  strip's smoke checks the gates through `Stats.RemovedBy`, not log lines. In game the strip's
  open/close lines are verbose-only, so they never drain it.
- HudFrame.Now is MISSION time: with `SlowDownOnOrder` the refresh (HudRefreshSeconds) and the
  formation stats run 4x slower in real time while the menu is open - fine for values; the cells are
  placed in `OnLayerFrame` every frame and the grace timers use the frame's real `Dt`.
- The first open of a battle reads size 0 for a frame (the cards were never laid out) → NotYet
  (cells hidden), placed the next frame.

**UNVERIFIED (PLAYTEST §8) + settling lines.**
1. The live widget tree reports the pixels we expect (GlobalPosition / Size of the generated card
   widgets) and our Scaled* bindings land the cells there: `first placement under the cards … cards
   drawn: 1 Infantry at (20, 44) 131 x 223 … cells: 1 Infantry at (20, 268) w 131` + Anton's eye
   (PLAYTEST 8a, 8d). If the cells are offset by a constant, compare the card numbers with the
   prefab maths above (1080p: columns at x 20 / 1769, y 44 + 263·k).
2. RTS Camera Command System's cards are found and matched: `8 cards in 1 set … (one layout - an
   order-menu mod such as RTS Camera Command System)`, every card `(= formation)`, Infantry at the
   bottom left (PLAYTEST 8c). Its ButtonWidget cards still take their clicks (our layer: no events).
3. The ItemTemplate list over a plain `Widget` instantiates 8 cells and 8 rows, and the ChangeWidget
   band draws from low to high (`CustomChangeColor`): the eye (8a, 8f).
4. The numbers fit beside vanilla's order icons at size 13 (≈ 34 px each side of a 60 px icon
   pair): the eye; the five `OrderStrip*` settings move them live.
5. The fallback panel never needed in vanilla or RTS Camera: the [summary] strip line `fallbacks:
   none`, `under the cards in` = `opened`.
6. War Sails naval battles (NavalOrderBar, same slot structure; its cards' counts vs a ship
   formation's) - untested; a mismatch would show as the panel with its reason.

## Step 10a — review (DONE 2026-09-27)

The full findings list is `docs/REVIEW.md` (R1-R24: 1 major, 9 minors, 1 For Anton, 13 not a
bug). The lessons worth keeping for any later change:

- **Two queued decisions in one tick: resolve the first before judging the second.** At a
  swing's end the step-back roll and the pace hold are both only ASKED (the tick starts them).
  The hold used to skip a man whose step back was merely pending - but the tick refuses many
  step backs (the cap, a shield wall, no enemy near), and those men were then neither stepping
  back nor held (R1). Now the hold is queued anyway and `TickPace` (which runs AFTER
  `TickStepBacks`) drops it only for a man whose step back really started. Any new "X takes
  precedence over Y" between queued actions: decide in the tick, in order, not at the ask.
- **A record list keyed by agent must forget the same way on every path.** Leaving the field,
  a stale record at a reused index, a deleted agent: all go through `Forget()` (loop entry, step
  back, pace hold, slowed horse; no engine call on the old agent). `OnAgentDeleted` is the
  backstop. A list entry the tick keeps working on after its agent is gone is how an engine call
  lands on a dead (or someone else's) agent (R5). Same for "waiting" pace holds: a man already in
  `_paceHeld` must not be added twice (R2).
- **Teardown code must survive its own failure.** Anything that must happen at the end
  (`StopAthletics`, the summary's once-flag) goes in a `finally` or before the risky part, and on
  the teardown fallback nothing reads an agent's native side (R3, R4).
- **The views are gone before the summary.** `Mission.EndMissionInternal` calls the listeners'
  `OnEndMission` first; `MissionScreen.OnEndMission` finalizes AND unregisters every view - so no
  view ticks on cleared agents even though `IsMissionTickable` stays true once `MissionEnded`
  (R10). Agents are cleared (`Agent.Clear()` zeroes every native pointer: `IsActive()` would be an
  uncatchable access violation) only after `OnEndMissionInternal`.
- **The log holds one handle now** (R6): `TraxLog` keeps a `StreamWriter` with `AutoFlush`
  (~7 µs a line instead of ~110 µs for open / append / close; measured), shared read / write /
  delete, released at every mission end (after the summary) and at unload, reopened by the next
  line, re-opened when `ModPaths` changes. Anything that READS the log while the game runs (the
  offline smoke's `LogText`) must open it with `FileShare.ReadWrite`.
- **Log volume with VerboseLogging on**: ~10 buckets × 20 lines/s × ~200 bytes = 30-50 KB/s in a
  big battle, so the 2 MB cap trims every ~35 s and earlier battles' summaries are lost (R7 - For
  Anton, not changed). Budget any new verbose bucket against that.
- **Deferred**: verbose lines are built before the rate limiter drops them (R8, ~30 call sites);
  the LATER features' settings that do nothing (R21 - step 10b). *(All three closed in step 10b.)*

## Step 10b — polish (DONE 2026-09-27)

Step 10 was split: 10a the review, 10b this polish before Anton's one playtest. Balance is NOT
touched here - Anton's numbers stay; the doubts go to him (TASKS_DONE entry / the manager's report).

**No switch that does nothing (R21).** The six settings that served only the LATER features -
`ShowTargetBar`, `TargetBarMaxDistance`, `TargetBarLingerSeconds` (step 7) and
`ShowFormationBars`, `FormationBarsAlways`, `FormationBarHeight` (step 8) - had no reader; they
left SettingsSchema, TraxSettings and defaults.json (so MCM and config.json). 63 → 57 (+1 below
= 58). The designs live in DESIGN §3 items 2-3 (marked LATER), their rows in DESIGN's "Planned
parameters" (a "When" column: LATER (step 7/8)), the recipes in "Step 7" / "Step 8" above. No
code existed only for them: the HUD plumbing (TraxHudView, HudGate's view condition, the read
API, the formation stats) is shared with the bar and the strip; the two commented
`AttachHudView` lines in `AthleticsLogic.Hud.cs` stay as pointers. An old config.json that
still has the six keys keeps them in its "Not recognised" section and logs `file problem:
"ShowTargetBar" is not a setting of this version - ignored (typo?)` at every read (game and
mission start) until they are deleted - harmless; PLAYTEST starts from a fresh config.json, and
no player has the mod yet.

**Log survival (R7 - the manager's decision).** Core `LogTrim` (pure, 11 tests), used by
`TraxLog.Trim`:
- An ENTRY = a stamped line + the unstamped lines under it (an error's indented stack, a message
  with a line break). KEPT = every NON-verbose line - load, config, mcm, mission, summary, error,
  [log] notes, and every feature's "mission start" / "first … this battle" / "YOU …" line - plus
  any [load] [compat] [config] [mcm] [mission] [summary] [error] line even if verbose. CUT = the
  oldest verbose lines, at ONE point in time (a big newer one that does not fit is not skipped to
  keep a smaller older one). Last resort, only when the kept lines alone pass the target: their
  oldest go too - the file carries over between game starts, so it must stay bounded.
- Why a mark and not the manager's tag list alone: the per-feature first-time lines (`[hud] orders
  strip: first placement …` - THE alignment proof, `[rate] first pace hold …`, `[athletics] YOU …`)
  share their tags with verbose lines; by tag alone they would be cut ~2 minutes into a verbose
  big battle. Verbose lines are written `stamp ~[tag] message` (`LogTrim.VerboseMark`); anything
  that greps "[damage] …" still finds both kinds (the smoke's checks were untouched).
- The note: ONE line at the top, `[log] (log trimmed at <t>: N older verbose lines cut, up to <t>;
  every other line (…) is kept)`; earlier notes (and step 3's "(older lines trimmed") are dropped.
- `LogMaxMegabytes` (Advanced, 1-100, default 8, live - `TraxLog.MaxBytes` reads it per line): the
  trim fires past it and keeps half. Measured (net8, scratch bench): 8 MB read + trim + write
  ~50-90 ms - a hitch every ~4 MB written (~100 s of a verbose 1000-agent battle); the game's
  .NET Framework may be slower. A failed trim (another program holds the file) retries after
  another 1 MB (`_trimRetryAt`), not at every line.

**Verbose lines built only when written (R8).** `RateLimiter.Peek(tag, now)`: true takes NO token
(the Verbose call that follows takes it); false counts the line as suppressed exactly like a
built-and-dropped one, so the "(+N similar lines suppressed)" notes stay exact (another thread
taking the token between the two = the old behaviour, built then dropped, counted). Wrapped as
`TraxLog.VerboseWants(bucket)` = VerboseLogging on && Peek. **The pattern for any new verbose
line**: `if (TraxLog.VerboseWants("my-bucket")) TraxLog.Verbose("tag", Build(), "my-bucket");` -
the SAME bucket both times, and a guard like `!st.IsHero &&` BEFORE VerboseWants (a "no" counts).
25 call sites converted; `VerboseOn` is now used only inside TraxLog.

**What the player reads.** Groups: Master switch, Damage randomness, Athletics, Tired fighters,
Tired fighters step back, **Refill** (was "Regeneration" over "refill" labels), **Your Athletics
bar** (was "Bars - you and your target"), **Orders menu strip** (was "Bars - your squads"; now
opens with its switch), Advanced. One vocabulary (the schema's header comment): Athletics / the
Athletics skill / the peak line (the white mark; label "Peak line (% of the bar)") / empty; a unit
in every label. `ParamDef.HintText` appends "Applies at once, even mid-battle." to every live
setting (the descriptions lost their scattered copies); the config file keeps its header's rule
(hand edits: next battle start). KEYS were not renamed - old config files keep working.
- **MCM's group order** - verified in the UI's decompile (`Bannerlord.MBOptionScreen.v1.4.8.dll`,
  `UISettingsUtils.SettingsPropertyGroupVMComparer`: the default group first, then `Order`
  ASCENDING, then the name). Trap: MCMv5's own `GetSettingPropertyGroups().SortDefault()` sorts
  by Order DESCENDING - do not read the page order from the abstractions. McmBridge gives a group
  2 × its schema order and the Defaults buttons Advanced's − 1: Master switch first, the buttons,
  Advanced last. The smoke checks the Order values.
- No default was changed. Sanity check: every default inside its range (the schema test), units
  right. Balance doubts listed for Anton in TASKS_DONE's 10b entry.

**PLAYTEST** is one ordered session now (A-G, ~2 h, VerboseLogging ON throughout, "What to send
Claude" at the end) with the line reference in its appendix (L1-L8); its last lines map the old
section numbers (§1-§8, 3n, 8a…) that AI_NOTES / RESEARCH / TASKS_TODO still cite.

**Gotchas met.**
- A bash heredoc holding a Python script with `r'''` strings broke the shell's quoting - write
  such a script to the scratchpad with the Write tool and run the file.
- After ANY schema wording / order change: `dotnet run --project tools/DefaultsTool -- refresh`
  (DefaultsFileTests compare every // line with what the mod would write).

**UNVERIFIED (PLAYTEST).** The page's group order in game (A5 - verified in the decompile and by
the smoke's Order check only); the trim's hitch in a real verbose battle (note the times).

## Step 11 — Steam packaging (DONE 2026-09-27 — package ready, NOT uploaded)

**The release is one command + one uploader run away** (`tools/WORKSHOP-UPLOAD.md`); the upload
waits for Anton's yes after his playtest. Nothing was uploaded, posted or published.

**Built**
- `tools/package.ps1` (ported from the sibling, stricter): manifest gate → build → unit tests →
  AssemblyGuard on BOTH DLLs (hard; the sibling's MCM check was only a warning) → OfflineSmoke → DLL
  version = manifest → `dist\TraxCombatEnhancements` from scratch → the file set checked against an
  explicit list → `dist\TraxCombatEnhancements_vX.Y.Z.zip` → file list + sizes. v0.1.0: 7 files,
  508,541 bytes (the two DLLs 172 + 215 KB, their pdbs ~50 KB each, two prefabs, SubModule.xml); zip
  210,539 bytes. The build output also holds `Newtonsoft.Json.dll` (Core's package reference, copied
  local) - the explicit list is what keeps it out.
- **The zip is written with ZipArchive, not Compress-Archive**: Windows PowerShell 5.1's
  Compress-Archive stores `\` in entry names, which non-Windows unzip tools turn into file names.
  Entries: `TraxCombatEnhancements/…`, the module folder at the zip's root.
- **The version has one home**: `Directory.Build.props` reads `module\SubModule.xml`'s
  `<Version value="vX.Y.Z" />` with an MSBuild property function (`File.ReadAllText` + a
  `Regex.Match(...).Value` lookbehind) into `TraxModVersion`; both csprojs set
  `<Version>$(TraxModVersion)</Version>`; an empty match fails the build (`TraxCheckModVersion`).
  So "bump the version once" is literally one edit, and the `[load]` line, the launcher and the zip
  name agree. package.ps1 re-checks the built DLLs against the manifest anyway.
- **Workshop kit**: `WorkshopCreate.xml` (Private, tags Utility / UI / Native / Singleplayer /
  v1.4.8 - Bannerlord's Workshop has no "Gameplay" type; its Type tags are Graphical Enhancement,
  Map Pack, Partial Conversion, Sound, Total Conversion, Troops, UI, Utility, Weapons and Armour,
  read off the browse page 2026-09-27), `WorkshopUpdate.xml` (ITEM_ID placeholder),
  `STEAM-DESCRIPTION.bbcode` (4282 bytes of Steam's 8000, tags balanced), the preview
  `preview_thumbnail.html` → `.png` (1024², 648 KB; headless Edge `--headless=new --screenshot`; the
  bar colours are AthleticsBar's constants; no game art). README: Requirements + Install. DESIGN §5:
  one copy runs, save-safe. PLAYTEST: A1's `[load] module:` line, A6 (optional) = the release package
  beside the dev copy.

**The uploader, decompiled again** (`TaleWorlds.MountAndBlade.SteamWorkshop.exe`, v1.4.8, ilspycmd
into the scratchpad) - the siblings' quirks hold, plus:
- `LoadTasks` iterates `FirstChild.ChildNodes` and calls `LoadFrom` on whatever task it made - a
  comment DIRECTLY under `<Tasks>` gives a null task → NullReferenceException → "Application crashed".
  Comments are fine inside UpdateItem (skipped as XmlComment), GetItem (only `ItemId` is read) and Tags.
- Start-up refuses without Steam running + logged in + **Steam Cloud enabled for the account AND for
  the app** (`IsCloudEnabledForAccount` / `IsCloudEnabledForApp`).
- `GetItemTask.DoJob` = `Convert.ToUInt64(ItemId)` BEFORE the update task runs: the `ITEM_ID`
  placeholder fails safe (FormatException, nothing uploaded).
- `UpdateItemTask`: title = `ModuleInfo.LoadWithFullPath(ModuleFolder).Name`; description, visibility,
  tags, preview only when their node is present (tags REPLACE the list); change notes default
  "Minor changes."; attribute values via XmlDocument, so `&#177;` / `&quot;` entities work.
- `ExitProgram` always `Environment.Exit(0)` after a `Console.ReadKey()` - which throws without a
  console: judge by the output ("Uploading done!", "Item created. Item ID is …").

**The dev and the release copy together (the brief's item 2) - built.** Findings (v1.4.8 source):
- `Module.LoadSubModules` loads each module's DLL with `AssemblyLoader.LoadFrom` →
  `Assembly.LoadFrom`; on .NET Framework a second file with the SAME identity (both 0.1.0.0) returns
  the FIRST assembly. `AddSubModule` then constructs a SECOND `SubModule` instance of it, and the
  loader calls `OnSubModuleLoad` on every instance in module order, on the main thread. So with the
  same version: one assembly, shared statics, two instances; with different versions: two assemblies.
  Both ran every hook before: two `TraxDamageModel`s stacked (two rolls per hit) and two
  `AthleticsLogic`s per mission (Athletics charged twice) - the smoke reproduces the double decorator
  when the guard is removed.
- `Core/SingleCopy`: the claim is an AppDomain data slot (`object[] { token, refused }`), shared by
  one or two assemblies; the token is the SubModule INSTANCE, so two instances of one type are two
  copies. The first to claim runs; a refused one sets the instance flag `_inert` and every hook
  returns at once - it must not even write the log (with two assemblies it would open a second
  writer on the file the running copy holds). The running copy logs `[compat] N copies of this mod
  are enabled (…) - this one (…) loaded first and runs` at load and, at the main menu (else the first
  game start), `[compat] 1 other copy … stood down` + ONE yellow message; `WARNING … none stood down`
  if two ids are enabled but nobody was refused (an older build without the guard).
- Which copy is "this one": `SubModule.xml`'s `<Id>` two folders above the DLL (a Workshop copy's
  folder is its item NUMBER, so the folder name alone would not say). New `[load] module: <Id>
  (the release | the dev install)` line - the playtest log now says which copy ran.
- Duplicate prefabs (both copies ship `TraxPlayerAthleticsBar.xml`): `WidgetFactory` hits
  `Debug.FailedAssert` ("This prefab has already been added") and keeps the LATER file;
  `MBDebugManager.Assert` is empty in the game, so it is silent. Harmless at the same version; after
  a prefab change the running DLL may get the other copy's prefab (the HUD fail safe would disable
  the view with an `[error]`) - one more reason the message says to disable one.
- No engine-managed (`DotNetObject` / `ManagedObject`) types in our DLL, so `Managed.AddTypes` sees
  nothing twice.

**Save-safe claim verified** (for the Steam page): no `SaveableTypeDefiner`, `[SaveableField]`,
campaign behaviour, `SyncData` or Harmony anywhere in `src`; the models are registered per game
start and the logic lives per mission.

**Gotchas**
- `-notmatch` does not fill `$Matches` - package.ps1 uses `-not (... -match ...)`.
- Package AFTER the release commit: the informational version carries HEAD's hash
  (`0.1.0+7c79099…` for the test package of this step).
- A dist zip of the current version blocks the next package run (the sibling's release-rhythm guard);
  the step-11 test package left `dist\TraxCombatEnhancements_v0.1.0.zip`, so the first real package
  of v0.1.0 needs `-Force` (a bumped version does not).

**UNVERIFIED (PLAYTEST A6 + the first upload)**
1. The real launcher and loader with both copies enabled: one `[load]` block, the `[compat]` pair, one
   message (the smoke drives two real SubModule instances, not the game's module loader).
2. That the uploader accepts the dist folder, the PNG preview and the UI tag (the siblings' uploads
   proved the folder shape, a JPG preview and the Utility tag).
3. How the Workshop page renders `STEAM-DESCRIPTION.bbcode` (h1, lists, the ± and × characters).

## Step 12 — playtest fixes, round 1 (DONE 2026-09-27)

Anton's first playtest session (`trax_combat.log`, 20:58-21:14, three game starts) found two bugs;
a third report (the exit hang) is not ours - notes below, no code.

**Fix 1 - the player bar outside battles.**
- Why there was no bar: the training field (scene `training_field_2`, StoryMode) runs in
  `MissionMode.StartUp` (Conversation while he talks to the trainer); HudGate allowed Battle / Duel /
  Tournament / Stealth only → `[hud] player bar: not shown at 0.1 s - not a fight (mission mode) - mode
  StartUp`, although Athletics ran there (37 blows charged, 15 hits rolled - the 21:06 summary).
- Which missions run in which mode (v1.4.8 SandBox, its `SetMissionMode((MissionMode)N)` calls):
  StartUp (0) = town centre, houses and indoor scenes, arena PRACTICE, the alley handler, a stealth
  zone's exit; Conversation (1) = conversations, the arena master; Battle (2) = town fights
  (`MissionFightHandler`), alley fights, the hideout ambush, combat-with-dialogue, tournaments' fight
  controllers; Stealth (4) = hideouts, prison break, sabotage; Tournament (7); CutScene (9) = the
  hideout boss intro. The training field: StartUp (the log).
- THE RULE (Core `HudGate`, pure, tested): fight modes as before. A view with the OUTSIDE-A-BATTLE rule
  (`HudOutside`; the player bar only - the orders strip keeps the fights-only gate) also shows in the
  WALK-ABOUT mode (StartUp only; Conversation, Barter, Deployment, Replay, CutScene and Benchmark are
  menus or films), with the player on the field and his Athletics tracked, while `weapon drawn ||
  below full || grace`. Check order: ModOff, AthleticsOff, ToggleOff (ShowPlayerBar), HideBattleUI,
  PhotoMode; then outside a fight: no rule or not the walk-about mode → NotFightMode;
  ShowPlayerBarOutsideBattles off → OutsideBattlesOff; NoPlayer; NotTracked; OutsideIdle. `HudShow`
  says why it IS up (fight / weapon drawn / refilling / grace) for the log.
- Decisions (written down as the brief asked):
  - **"A weapon drawn" = anything wielded in either hand** (`HudFrame.HandsFull`:
    `GetPrimaryWieldedItemIndex() != None || GetOffhandWieldedItemIndex() != None` - the game's own
    hands-empty test, Agent.cs's swimming check; two reads from the agent's native memory, no
    allocation). A shield alone counts (he is ready to fight). **Fists only = not drawn** - fists are
    no item, both hands read `EquipmentIndex.None`, so it IS distinguishable. A torch or a banner in
    hand would count too - accepted.
  - **"Below full" = below the top he can REFILL to** (`AthleticsReading.BelowFull`: fraction < usable
    fraction − 1e-6), not below the whole pool - else a wounded player's bar would never leave in town.
  - **A 1 s grace** (`HudGate.OutsideLingerSeconds`, a documented plumbing constant, not a setting): a
    weapon switch sheathes one and draws the other, both hands empty for a moment; without it the
    layer (a movie load) would be torn down and rebuilt at every switch. It only KEEPS a shown bar
    (`Lingers(shownNow, …)`), never brings one back.
  - Mode changes follow on the next frame: a town fight (Battle) ending with empty hands and a full bar
    → gone; a conversation → gone, back after it if the weapon is still in hand.
- Setting `ShowPlayerBarOutsideBattles` (true, live; group "Your Athletics bar", label "Your bar outside
  battles too") - 59 settings.
- Log: builds / removals outside a fight in their OWN rate bucket `hud-outside` (a burst of 30, then 1/s
  - a town walk never eats the battle lines' `hud-layer` budget), each with its reason: `layer created
  at … (mode StartUp, outside a battle: a weapon drawn, was hidden: …)`, `layer removed at … - outside a
  battle: no weapon drawn and your Athletics full`; a changed reason while up (the weapon put away while
  it refills) verbose only (`hud-outside-why`); the attach line tells the rule; the summary's clause
  `outside a battle: shown Nx (weapon drawn N, refilling N), on screen N s` (`HudStats.HasOutsideRule`).
- Cost: in fights nothing new (the outside facts are read only outside a fight); outside one, two
  pointer reads and one `TryGetReading` per frame, no allocation.

**Fix 2 - MCM's retry loop.**
- The 21:08 session: `Bannerlord.MBOptionScreen` NOT in the module list, yet `[mcm] MCM 5.12.2.0 is
  loaded but not ready yet at retry - retrying every 1 s.` and never registered - another enabled mod
  ships an MCMv5 DLL in its own bin (5.12.2, while the Workshop MCM is 5.12.3; which mod was not
  identified - the log lists modules, not assemblies). MCM's services are built only by its own
  module's SubModule (`MCMSubModule.OnBeforeInitialModuleScreenSetAsRoot` →
  `GenericServiceProvider.GlobalServiceProvider = ….Build()`, 5.12.3 decompile), so without the
  module `BaseSettingsBuilder.Create` returns null forever.
- Learned on the way: those services are built at MCM's MAIN-MENU hook, the same frame as ours. The old
  tick retried from the very first application tick, so the pre-menu attempts were always doomed (every
  session registered at "main menu (attempt 3)").
- Now (Core `McmPlan` + `McmBridge`): the module list of the load line → `McmBridge.UseModuleList`; no
  DLL → the old "not loaded" line; DLL loaded but MCM's module off → ONE line "MCM's module is not
  enabled - no settings page; config.json only. (…)" and no attempt; the first attempt at the main menu
  (or the first game start); after "not ready" the tick retries 1/s up to `McmPlan.MaxRetries` (30 - a
  documented CONSTANT, not a parameter: no player tunes it; MCM is ready at worst one retry after the
  main menu when our module loads before MCM's), then ONE "never became ready - gave up after 31
  attempts" line and silence. An unknown module list (read failed; the smoke) = just try, capped.

**Exit hang (no code) - Anton: the game hangs on "shutting down" in Steam after exit.**
- Our side is finished by then: session 1 logged `[load] unloaded (game closing)` at 21:06:04.694, 4 s
  after the last mission's summary (21:06:00.152); session 3 the same (21:14:10.629, 4 s after 21:14:06).
  Session 2 (21:08, the one without MCM's module) has NO unload line: its last line is the game start
  at 21:08:18 (no mission ran), the next is session 3's load at 21:11:48 - that game ended without the
  loader's unload call (killed, or a crash). Worth asking Anton which session hung.
- Verified 2026-09-27 (grep of `src`): no `Thread`, `ThreadPool`, `Task`, `async` / `await`, `Timer`,
  `Process`, `BackgroundWorker` or `Parallel` anywhere. The only threading: short `lock`s around
  in-memory counters and the log writer, `Interlocked`, one `[ThreadStatic]` Random; the `Stopwatch`es
  are clocks with no callbacks. The mod owns nothing that could outlive the game.
- `OnSubModuleUnloaded` cannot block: it writes one line (the log's `Gate` lock - held per line by
  whoever writes; no thread of ours exists, an engine callback holds it for one line) and
  `TraxLog.Release()` disposes the one FileStream (AutoFlush - already flushed) inside try/catch.
  Nothing waits on anything.
- So the hang comes after our unload. Suspects: another mod's helper process or foreground thread
  (ImmersiveAI.Dev spawns helper processes), or the game's own `Watchdog.exe`. **Diagnosis**: next time
  it hangs, inspect the process tree WHILE it hangs (Task Manager → Details with the "Parent" view, or
  Process Explorer, or `Get-CimInstance Win32_Process | Where-Object ParentProcessId -eq <pid of
  Bannerlord.exe>`): which child of Bannerlord.exe - or which process holding it - is still alive. The
  BUGS line stays open until then.

**UNVERIFIED - only the game can tell (PLAYTEST F6, A1, A4)**
1. `HandsFull` in game: sheathed = None in both hands; a drawn sword / shield / bow = not None; a weapon
   switch's empty moment shorter than the 1 s grace (else the `hud-outside` lines show a blink).
2. Towns track the player like the training field did (`[athletics] YOU: …` in the mission's lines).
3. With MCM's module enabled: `registered at main menu (attempt 1)` (or `retry (attempt 2)`).
4. With MCM's module off and a carried DLL: the "module is not enabled" line once, no other `[mcm]` line.

## Step 13 — PAUSE ONLY: a no-attack timer instead of slow-mo (DONE 2026-09-27; the research below was written before coding)

Anton's playtest call (2026-09-27): the attack slow-down "feels strange, I start swinging in slow-mo".
His choice: animations at full speed; after each attack a **no-attack timer** of D × (1/m − 1) (D =
the attack's own duration, m = S + (1 − S) × f when it ends), for him AND the AI; a countdown ("1.3 s")
by his Athletics bar while it runs; the bar flashes when he tries to attack early. DESIGN §2 "PAUSE
ONLY" is the spec.

**What his log said about the old technique** (`trax_combat.log`, 21:06 / 21:14 / 21:26 summaries)
- Melee AI at the peak: wind-up 0.32-0.39 s, swing 0.40-0.52 s, pause 0.49-0.83 s, cycle 1.3-1.8 s.
  So D (wind-up + swing) ≈ 0.75-0.85 s and the AI's own gap between attacks G ≈ 0.6-1.0 s.
- With the old pace hold (aimed at the fresh cycle ÷ m) plus AI decisions × m, the tired melee AI read
  128% and 172% "too slow", its pause ×5-×10 - and **"the next ready came avg 1.5-2.6 s after a hold
  ended"**: a NoAttack hold costs the AI a re-decision of its own after the flag lifts (at m 0.8-0.9 the
  decision scaling alone would add only 10-25% to a 0.5-0.8 s pause). The old hold already delivered its
  target; the decision scaling and that latency came on top - a double count.
- Ranged AI (animations + decisions, no hold) read 102% on target: draw 1.25 + aim 0.97, loose 0.13,
  reload 1.19, pause 0.5, cycle 3.6 s at the peak.

**The player's attack input - where it is written and when the engine reads it (v1.4.8 source)**
- `MissionMainAgentController` (MountAndBlade.View, a `[DefaultView]` MissionView) writes the player's
  input in `OnPreMissionTick` → `ControlTick`: every frame `mainAgent.MovementFlags = 0`,
  `EventControlFlags = 0`, then ORs in what is held. Attack (game key 9) held →
  `MovementFlags |= AttackDirectionToMovementFlag(GetAttackDirection())` - one of `AttackLeft/Right/Up/Down`
  (`MovementControlFlag.AttackMask` 0x3C0); block (key 10) → the `Defend*` bits (0x3C00 / 0x7C00); the
  gamepad's alternative aiming ORs the attack bits too. **Kick (key 16) is `EventControlFlag.Kick`
  (0x8000) - not an attack bit.** Only when `CombatActionsEnabled` (a game object may disable it - it
  gates attack AND block alike, so it is no lever for us). `MovementFlags` is native (`IMBAgent.Get/
  SetMovementFlags`): holding = the engine readies (wind-up, then the blow held), letting go = the
  release, bits during a release = the next blow chained.
- No engine flag stops only the player's attacks: `AIScriptedFrameFlags.NoAttack` is read by the AI's
  decisions (the player's agent has no AI); `CombatActionsEnabled` also kills blocking.
- **Tick order** (Mission.cs): the native `Mission.Tick` (`IMBMission.Tick`) calls the `OnPreTick`
  callback FIRST - it waits for the previous frame's async agent tick, then runs every behaviour's
  `OnPreMissionTick` **in reverse list order** (`for i = Count-1 .. 0`) - and then does its native work
  (the agents' actions read their flags there, by the name and the order); after it the managed
  `Mission.OnTick` runs `OnMissionTick` (reverse order again) and starts the async agent tick.
  So clearing the bits in our `OnMissionTick` is too late (the native already started the wind-up), and
  our `OnPreMissionTick` runs BEFORE the controller's: `SubModule.OnMissionBehaviorInitialize` is called in
  `Mission.AfterStart` after every starting behaviour (logics, then views incl. the default views from
  `MissionScreen.OnAddBehaviors`) is in the list, so `AddMissionBehavior` APPENDS us - last in the list,
  first in the reverse loop, and the controller would overwrite whatever we cleared.
- **The lever**: a tiny behaviour (`PlayerAttackGate`) added with the public `AddMissionBehavior` (sets
  its Mission, joins MissionLogics, `OnCreated`) and then moved to index 0 of the public
  `Mission.MissionBehaviors` list - still inside OnMissionBehaviorInitialize, before `AfterStart`'s
  `foreach EarlyStart` enumerates it. Index 0 pre-ticks LAST, i.e. right after the controller wrote the
  frame's input and before the native reads it. While the player's timer holds, it clears
  `AttackMask` from `MovementFlags` (one native read + one write a frame, only while holding). The
  defend bits stay (blocking always works); `EventControlFlags` stay (kick, jump, crouch, wield, sheath,
  mount - all free). No Harmony, nothing patched. If the native read the flags later (in the async agent
  tick) index 0 still works - nothing writes them between our pre-tick and then.
- What the engine sees: attack bits never set during the hold → no wind-up at all (no half ready, no
  stuck state - the same as the button not being pressed). The button still HELD when the hold ends →
  the next pre-tick lets the bits through → the wind-up starts that very frame (hold-to-attack). A tap
  during the hold is swallowed whole (the spec: "pressing attack during the timer does nothing").
- Never clear the bits while the player is IN a ready: bits vanishing mid-ready is "button let go" = the
  release. So the hold may only begin when no ready runs: at the start of a release (the swing / the
  loose), never later.

**Decisions (Claude's, 2026-09-27 - Anton can overturn any)**
1. **D** = the attack's own duration: melee = its wind-up (the ready up to full wind-up - the held part
   of a readied blow is NOT counted, a player may hold one for seconds) + its release (the swing with its
   follow-through, up to the recoil or the pause). Ranged = draw (up to full) + loose + **the reload that
   follows** (nocking, a crossbow's winding, taking the next javelin) - the animation technique scaled
   ReloadSpeed too, and the reload is the bulk of a crossbow's effort (the AI archer's D ≈ 1.25 + 0.13 +
   1.19 ≈ 2.6 s). The timer starts when the attack ENDS: melee at the release's end (a block recoil
   plays inside the timer), ranged at the reload's end (or the loose's, if no reload follows).
2. **m** = the exact curve value S + (1 − S) × f when the attack ends (not the 0.05-stepped value the
   recomputes use). m ≥ 1 → no timer. A timer below **0.1 s** (`AttackTimerMath.MinTimerSeconds`, the old
   `MinHoldSeconds` - plumbing) is not started: not worth a flag write for the AI, and for the player it
   would swallow a chained blow just below the peak line for nothing he could see.
3. **The player's hold starts at the release's START** - a click during his own swing would otherwise
   queue a chained blow the engine starts before the timer exists (Release → Ready directly), and a
   player hammering the button would never be held. It begins only when this attack's timer is sure to
   be worth it: expected pause = (this attack's wind-up + his last measured rest of an attack of the
   same kind: the release, plus the reload for ranged) × (1/m − 1) ≥ 0.1 s, m after this blow's charge.
   At the attack's end the real timer replaces the estimate (below 0.1 s → the hold ends at once).
4. **Presses**: a press = the attack bits' rising edge while the hold is on (a button already held when
   the hold began is not a new press - it is hold-to-attack). Each is swallowed and counted; each flashes
   the bar (FlashBarOnEarlyAttack) unless a flash is still running (two pulses, 0.12 s on / 0.08 s off -
   a UI constant, like the bar's colours).
5. **Kicks are never held** (a separate key and flag; free, DESIGN interpretation 8). **Bashes are held**
   with every other use of the attack button: a shield bash is attack-while-blocking, and letting the
   attack bits through while the block bits are set would let a normal ready start the moment the block
   is dropped with the button still down. Still free of Athletics.
6. **Ranged**: the reload is never held (it is automatic; stopping it would need animation control);
   the next draw / aim / throw waits for the timer. A bow already drawn is never cancelled - the hold
   only ever begins when a shot is loosed, so no bow is drawn when it starts; a draw pressed during the
   timer does not begin, and a held button draws the moment it ends.
7. **The AI**: the pace hold becomes the AI's timer - NoAttack from the attack's end for D × (1/m − 1),
   **melee AND ranged, on foot AND mounted** (horse archers and lancers too: without the animation
   technique a rider would otherwise attack at the full rate, however tired - a regression). The flag
   hygiene stays (never over a game job, lifted only while free). The fresh-cycle reference, the
   expected-ready arithmetic and the rider refusal go. Still `AttackRatePaceHold` (A/B).
8. **`AttackRateAiDecisions` default → OFF** (the maths): the timer supplies D × (1/m − 1); the AI's own
   gap G stays at its fresh length plus the NoAttack re-decision latency (1.5-2.6 s measured). With the
   decisions on, G and that latency are also stretched × 1/m - both scale the SAME pause (the timer runs
   inside the AI's own gap from the attack's end), which is what read "too slow" in the log. Kept as the
   A/B switch. Anton's config.json carries `true` from the old default, so the config format goes to 2
   and a format-1 file's `AttackRateAiDecisions: true` is migrated to `false` once, logged.
9. **`AttackAnimationMinPercent`** (100): the stat decorator's animation multiplier is max(m, this/100) -
   100 = never slowed; lower brings a little slow-mo back on top of the timer. Run speed, the horse, the
   AI decisions: unchanged. Changing it mid-battle recomputes every tired fighter (budgeted).
10. **`AttackRatePlayerTimer`** (on): the player's own timer is its own switch - an escape hatch in case
    the input gate misbehaves in some mission; the AI's stays `AttackRatePaceHold`.
11. **The countdown and the flash** - REPLACED mid-step by Anton (via the manager, 2026-09-27): "above
    that bar add a bar 'attack recovery' that empties when I attack and until it fills I can't attack;
    inside it add the secs delay added". Built instead: the **Attack recovery bar** (its own view,
    `ShowAttackRecoveryBar`), the seconds ("1.3 s", tenths rounded UP so it never reads 0.0 while it
    runs) inside it, the flash on IT (`FlashBarOnEarlyAttack`) - a white overlay, updated every frame
    (OnLayerFrame), not at HudRefreshSeconds (a 0.1 s pulse would stutter). `ShowAttackCountdown` never
    shipped.
12. The target of the summary's verdict stays **the fresh cycle ÷ m** (DESIGN's "rate × m"); the
    technique's own check is new: per band the timer asked vs the measured gap from the attack's end to
    the next attack's start (must be ≥ the timer), attacks that started anyway (must be ~0), the latency
    after the timer ended, and the animation multiplier asked and measured (×1.00 by default).
13. **The recovery bar's place**: above the Athletics row there is no room - the vanilla horse bar sits
    at 80 px and the hero health bar's frame at 90 px from the bottom (AgentStatus.xml, Default state),
    our row at 54 (+ ~24 high). So the Athletics row moved to **30** (`PlayerBarOffsetBottom`) and the
    recovery row sits `RecoveryBarOffsetAbove` (24) above it - where the Athletics row was. Format 2
    moves a config.json holding the old 54. It moves with the Athletics bar; its right end = the
    Athletics bar's (`PlayerBarOffsetRight`).
14. **The recovery bar's rules**: the Athletics bar's gate and outside-a-battle rule, its own switch, and
    only while `ShowPlayerBar` and `AttackRatePlayerTimer` are on (with the timer off it would sit full for
    nothing). EMPTY from the release's start when the hold began there (the swing), FILLING over the
    countdown, FULL otherwise; amber while not full, steel when full.

**Built (DONE 2026-09-27)** - file map in CLAUDE.md "Layout"
- Core `AttackTimer.cs`: `AttackTimerMath` (Pause = D × (1/m − 1), Worth ≥ 0.1 s, AnimationMultiplier =
  max(m, min%), CountdownText, FlashOn - 2 pulses 0.12 / 0.08 s, the bar's colours), `PlayerAttackTimer`
  (AttackStarted → the hold from the release, AttackEnded → the countdown, Frame(now, pressing) → clear /
  swallowed / flash / ended / held-at-end, Release), `PlayerGateFrame`, `PlayerTimerEnd`,
  `AttackRecoveryReading` (Share, SecondsText, Recovering, the flash). `AttackRate.cs`: `AttackRateRules`
  + PlayerTimer / AnimationMinPercent / PlayerTimerOn, the new Describe; `PaceNotHeld` without Player /
  Rider / NoReference (+ NoDuration), `PaceEnd.AttackStarted`, no Mounted; the fresh-reference hold maths
  (ExpectedReady, HoldUntil, FreshReference, HoldNeeded) removed. `AttackRateStats`: the animation asked per
  band, the timer rows (D, m, asked, the gap, after the end, early), your timer's counters, the AI timer's
  kinds / mounted. `ConfigFile.FormatVersion` 2 + `Migrate` (format 1: AttackRateAiDecisions true → the new
  default, PlayerBarOffsetBottom 54 → the new default; a file without a stamp is not migrated).
- Settings 59 → 66: `AttackRatePlayerTimer`, `AttackAnimationMinPercent` (Tired fighters, reordered: speed,
  your pause, the AI's pause, the AI decisions, the animation floor), `ShowAttackRecoveryBar`,
  `FlashBarOnEarlyAttack` (Your Athletics bar), `RecoveryBarWidth` / `Height` / `OffsetAbove` (Advanced);
  defaults: AttackRateAiDecisions false, PlayerBarOffsetBottom 30 (DESIGN's table carries the same - a
  design change, not a tuning).
- Module: the phases build D and flag an attack's end (`TrackedAgent.AttackEndedNow`, `EndedDuration`);
  `ObserveAction` → `AttackEnded` after the step-back roll → yours (`PlayerAttackEnded`) or the AI's
  (`AiAttackEnded`, queued like the old hold); a ready / release begins → `AttackBegan` (the gap after the
  last timer, an AI hold slipped through, your hold missed); your hold at the release's start
  (`PlayerAttackStarting`, after the charge). `AthleticsLogic.PlayerTimer.cs`: the gate's frame
  (`GatePlayerInput` native → `GateFrame` managed), the countdown's end, hold-to-attack, releases
  (switched off, not you, mission end), the tick's safety net (a countdown the gate never ended is ended
  0.25 s late and counted), `TryGetPlayerRecovery`. `PlayerAttackGate` (index 0 of the list, attached in
  SubModule with a `[rate] attached: …` line naming the controller's index; checked again at the first hold).
  `GamePaceBody`: riders allowed; `MustEnd` = the player took him only. The stat decorator: animations
  × max(m, min%), AI decisions only with the switch; counts "attack" only when the animations were slowed.
  `TraxHudView`: each view's own rate buckets (`hud-layer:<view>`, `hud-outside:<view>`) - the two bars
  build and go together and one must not spend the other's budget (the smoke's fast run hit it).
- HUD: `AttackRecoveryView` + `AttackRecoveryVM` + `TraxAttackRecoveryBar.xml` (9 widgets: the row, the
  label, the frame, the inside, the fill, the seconds, the flash); attached after the player bar.
- Tests 323 → 333 (`AttackTimerTests` 10, the migration, the new rules / summary lines; 2 old hold-maths
  tests removed). Smoke 48 → 51 steps: the attack rate rewritten (full-speed animations; the AI timer -
  melee, ranged from the reload's end, a throw, riders; the gap = the timer; every lift path; YOUR timer
  through the real logic), the gate first in a stand-in mission's list, the recovery prefab, the recovery
  bar through the real view; the decorator step (the floor at 100 / 0 / 60, the AI switch), the master
  switch releasing your countdown. deploy.ps1 green.

**Numbers from the smoke's model** (the AI readies the moment NoAttack lifts, its own pause 0.4 s, D 0.82):
at empty the pause is 3.28 s and the cycle 0.9 + 3.28 = 4.18 s against the fresh 1.3 ÷ 0.2 = 6.5 s → 64%
"too fast" - the AI's own idle gap runs INSIDE the timer, as the D-based spec implies. In game the
NoAttack re-decision (1.5-2.6 s measured in step 12's log) sits on top; mild bands may read "too slow".

**UNVERIFIED - only the game can tell (PLAYTEST C1, C1b, C2, C4, C5, E1, L5, L7; the line that settles each)**
1. The gate clears the input before the engine reads it → `attack rate - your timer …: attacks that started
   while held anyway: 0`; your `… - timer:` rows `started before the timer ended: 0`; and in the hand: an
   early press does nothing (no half wind-up). `[rate] your attack gate holds for the first time: it
   pre-ticks after MissionMainAgentController (gate 0, controller K …)` - a WARNING there = the order broke.
2. Hold-to-attack: the held button starts the wind-up the frame the bar is full → `your attack began avg
   0.0x s after`.
3. Blocking, kicks, weapon switches during the pause (the eye - C1b 3); shield bashes wait (by design).
4. Ranged: the reload (nocking) is not held and the next draw waits → your ranged `timer:` rows; the eye.
5. NoAttack holds ranged AI and mounted AI as it holds melee on foot → `attack rate - AI timer ends: … an
   attack started anyway` ~0 with `ranged` and `mounted` holds above 0 in the `AI timer (…)` line.
6. The animations really play at full speed → every band `animations asked x1.00` and the phases `(x1.00)`;
   the `[rate] first slowed fighter …` line `(each x1.00 as asked - full speed, no slow-mo)`.
7. The AI-decision default (off): the C7 A/B - the melee AI verdict with it on vs off.
8. The recovery bar's place at 1080p and other UI scales (between the vanilla bars and our Athletics row,
   nothing overlapping) → the eye; the `first values pushed` line's numbers.
9. The flash reads as "not yet" without being annoying (2 pulses, no restart while one runs) → Anton.

## Step 15 — research: battle pacing (DONE 2026-09-27, no code)

Anton: battles end fast; "a way to slow them down a bit for tactical decisions or hero units to become
more powerful … like RBM but without the units overhaul"; "just let me see what is in there". **The
whole write-up is `docs/BATTLE_PACING.md`** - the menu of 11 levers (length / feel / hero power / cost /
risk / Harmony / conflicts), the recommended package, the baseline, RBM technique by technique (A1-A11),
the step back (B), our levers (C), compatibility (D), UNVERIFIED list. Decompiles (outside the repo):
`..\reference\RBM-decompiled\` (RBM v4.5.0.2, 6 DLLs), `..\reference\RTSCamera.CommandSystem-decompiled\`.

**The five findings**
1. **RBM's long battles come from formation-level AI plus its armour/damage rework**, nearly all hooked
   with Harmony into formation internals: a charge that keeps slots and faces targets
   (`OverrideFormationMovementComponent` on `HumanAIComponent.GetFormationFrame`, Charge → ChargeToTarget),
   charging men who only want melee within ~2 m (`OverrideHumanAIComponent`: Melee 5.5 / 2 m 1 / 10 m 0.01
   vs vanilla 8 / 7 m 4 / 20 m 1), per-man "frontline" micro (`Frontline.cs`), culture battle plans that
   advance as formations until 75 m (`Tactics.cs`), and `ArmorMultiplier` 2 in its own damage formula.
   Its per-man AI values (`AgentAi.OverrideSetAiRelatedProperties`) are NOT simply more defensive (a
   skill-20 recruit blocks at 0.30 vs vanilla's 0.62). RBM has no rotation of tired men.
2. **RBM's BackStep**: a prefix on `Formation.GetOrderPositionOfUnit` returns a spot 0-0.3 m behind the
   man and locks his position there (`Agent.SetTargetPosition`, cleared at the next query), re-decided at
   every formation-frame refresh (~0.5 s, `Agent._cachedAndFormationValuesUpdateTimer`), charge orders only,
   formation frame switched off in a charge; no direction is set - the combat AI keeps him facing because
   the target is never more than a shuffle away. **Ours turns** because `SetScriptedPositionAndDirection`
   = `GoToPosition` navigation (item pickup, ladders): a navigating man faces his path and the direction
   applies on arrival - 2 of 2159 arrived (inferred from the log; the native code is not visible).
3. **The public fix**: `AgentComponent.OnAIInputSet(ref EventControlFlag, ref MovementControlFlag, ref Vec2
   inputVector)` (public virtual; the engine calls it for agents with `Agent.SetHasOnAiInputSetCallback(true)`;
   `inputVector` in the man's own frame) - write "backwards" for the step back (a backpedal facing what the
   AI faces), clear only `AttackMask` and keep a defend bit up. RTS Camera Command System uses exactly this
   (`CommandSystemAgentComponent`, `AgentAIInputHandler.OnAIInputSetForDefensiveHold`, `SetCancelAttack` =
   clear AttackMask + DefendDown) and turns the callback on for EVERY agent - never switch it off. The same
   hook can replace `NoAttack` in the AI timer, and with one component holding the attack bits until the
   later of the pause and the step back ends, the pause survives a step back (BUGS "Empty AI attacks too
   fast": 2.2 s cycles at empty vs 8.5-9.3 s; 976 pauses "not held: stepping back"). Fallbacks: RBM-style `SetTargetPosition` hops; a rank swap
   (`Formation.SwitchUnitLocations`, public) for holding lines; the unused `Drag` scripted flag
   (`SetDraggingMode`); 5d's hang-back behaviour values.
4. **The log: tired men are defenceless**, and that is our biggest accelerant: blocked 2-13% while held by
   the AI timer's `NoAttack`, 4-6% while stepping back (80% back turned mid-step, ~0.6 m of 2 m), vs
   33-45% for everyone else; in the 240v240 **41%** of landed melee hits struck men in those two states;
   Athletics empties within ~10 s of contact (your 207 infantry: f 0.22 at +10 s, 137 dead at +37 s). So
   today more exhaustion = a SHORTER melee. (UNVERIFIED as cause: the held men's 2% could partly be who
   gets held.)
5. **The cheap sure levers are model-level**: a troop-only damage scale in `TraxDamageModel` (melee ∝ 1/k,
   heroes exempt → heroes stand out), troop caution through the stat decorator (`AIAttackOnDecideChance` /
   `AIAttackOnParryChance` × k - vanilla already does 3× by order via `Agent.Defensiveness`), a
   `BattleMoraleModel` decorator (abstract, public), armour × k into
   `StrikeMagnitudeCalculationModel.ComputeRawDamage`. Behaviour values (`agent.SetAIBehaviorValues`) and
   rank swaps are public but only for HOLDING lines (a vanilla charge gives a man no slot) and must be
   re-applied on every order change; giving charging men slots needs Harmony.

**Recommended** (for Anton to pick): #2 the guard really up (fixes the step back and the AI timer's guard),
#1 troop damage scale (start at 0.75), #3 troop caution as an A/B, then #4 rank rotation or #5 front ranks
only for the tactical feel. Measure each with the existing summary lines (facing / moves / guard, damage,
duration).

**Compatibility notes for whoever builds it**: RTS Camera Command System patches `MovementOrder.GetSubstituteOrder`
/ `GetPositionAux`, `Formation` spacing and tick, `ArrangementOrder`, `FacingOrder`,
`HumanAIComponent.GetDesiredSpeedInFormation`, the order UI - NOT `GetOrderPositionOfUnit`,
`GetDirectionOfUnit`, damage, stats or morale; its volley / defensive-hold modes rewrite AI input and set
behaviour values on their own state changes (overlaps with #2 and #5).

## Step 14 — run-speed floor 0.7 + the refill curve (DONE 2026-09-27)

Anton, night of 2026-09-27, asleep while it was built (no questions): after his 240v240 "make them slow down
to 70% speed" (0.3 was "too slow, unrealistic"), and "recover faster when it's low and slower as it is
fuller by some modifier, not crazy, maybe half linear". DESIGN §2 (run speed; "Faster when low, slower when
full" with the small table) and interpretation 17 are the spec.

**Built**
- `MinMoveSpeedMultiplier` 0.3 → **0.7** (defaults.json AND DESIGN's initial value - a design change, as step
  13 did). **Config format 3** (`ConfigFile.Migrate`): a format ≤ 2 file still holding the old default 0.3
  gets 0.7 once - `[config] migrated config.json at …: MinMoveSpeedMultiplier: 0.3 → 0.7 (format 2 → 3, the
  old default; step 14: …)` - and is rewritten as format 3 (ConfigStore, unchanged). Checked on the real DLL
  against Anton's own config.json (read only): exactly that one note, `RegenRateNearFullPercent` missing
  (added with its default), nothing else moves. A format-1 file gets step 13's two AND this.
- **The refill curve**, new setting `RegenRateNearFullPercent` (Refill group, int 10-100, default 50; after
  `FullRegenSecondsStanding`): rate(x) = r0 × (1 − (1 − k) x) × the effort multiplier, x = Fraction (share of
  the FULL pool), k = % / 100, r0 = ln(1/k) / ((1 − k) T) so 0 → 1 at a walk takes T. Core `AthleticsMath`:
  `RegenRateAtEmpty`, `RegenCurve`, `RefillFrom` (exact: x0 + (1 − a x0)(1 − e^(−a r0 m t)) / a, a = 1 − k),
  `RefillSeconds` (ln((1 − a from) / (1 − a to)) / (a r0 m)); `Regen` uses them (the top reached: used =
  RefillSeconds to the top), `RegenFractionPerSecond` = r0 × curve × effort. `AthleticsRules.RegenNearFullShare`
  clamps k to 0.01..1.
- Summary: the settings sentence ends `…, near full at 50% of the rate near empty (at a walk: half the bar in 25
  s, the peak line in 41 s)`; the regen line ends `refill curve: near full x0.50 of near empty (…), x1.39 →
  x0.69 of a flat refill` (its "at the full rate" became "at the walking rate (x1)" - "full" was ambiguous next
  to a curve); NEW `Athletics refill from empty to the peak line (no blow between): N runs, avg X s (fastest,
  slowest) - 40.7 s at a walk …` (`RegenOutcome.EmptyToPeakSeconds`: set on the step that crosses the line of
  a refill run that began at 0, exact to the crossing). `YOU are back at full strength …` adds `- up from empty
  in N s of refill (…)`; the refill line names the curve. The effort-tenths line is unchanged (it bins effort,
  not fill - it reads right as it was).
- Tests 333 → 351 (the migration; T for k 10-100 and several T; DESIGN's table 24.9 / 40.7 / 19.3; k = 100
  bit-identical to step 5c's arithmetic over 3000 random steps with blows, wounds and efforts; boundaries;
  monotonic; one 30 s step = 300 steps of 0.1 s; the empty-to-peak report and what it leaves out; the words).
  Old tests that asserted flat numbers now either compute the curve's value or pin `nearFull: 100` where they
  test other arithmetic (the delay straddle). Smoke: DESIGN's 0.7 and 50 set explicitly; the run checks moved
  to 0.7 (+ 0.3 × f; an empty man's MaxSpeedMultiplier 0.8 × 0.7 = 0.56). deploy.ps1 green.

**Decisions (Claude's - Anton can overturn any)**
1. "Half linear" = the rate near full is HALF the rate near empty, a straight line in the fill between (k 0.5).
   The rate at empty is ×1.39 the old flat rate, near full ×0.69: "not crazy".
2. T keeps its meaning (empty → full at a walk), so the curve only moves time from the top of the bar to the
   bottom: the peak line 45 → 40.7 s, the last quarter 15 → 19.3 s. A tired man is back in the fight sooner.
3. The fill is on the FULL pool, not the usable one: a wound does not change the rate at a given bar level,
   and a man refilling to a low cap refills on the fast part of the line.
4. Exact integration per step, so the regen step (0.1 s, engine plumbing) never changes a refill time, and
   100 reproduces the old rule to the bit (the flat branch is the old arithmetic, same operation order).
5. The slider stops at 10: k → 0 never reaches full (r0 → ∞), above 100 would refill slower when low. Int
   percent, like the other *Percent settings.
6. The migration is the step-13 precedent: only a value equal to the old default moves (a hand-set 0.3 cannot
   be told apart and moves too; the log line names it); no other value of his is touched. His file today is
   format 2 with 0.3 → his next game start migrates it.

**Side effects to know**
- With the floor at 0.7 the run multiplier spans only 0.3, so it moves in fewer 0.05 recompute steps (~6 from
  empty to the peak instead of ~14) - fewer `UpdateAgentProperties` calls, nothing else. The run speed check's
  "follows the curve" still has signal: its f 0.5-1 / below 0.5 / empty rows ask ~0.92 / ~0.78 / 0.70 (< 0.97).
- A near-empty man's top is now ≈ 0.7 × 4.5 ≈ 3.2 m/s, above a formation's 1.8 m/s walk: keeping up is effort
  ~0.57, not a flat-out run (step 5c's note about x0.3 no longer applies) - he refills at ~x0.86 while walking
  with his formation.

**UNVERIFIED - only the game can tell (PLAYTEST A1, C1, C6, L1, L4)**
1. The migration on Anton's real file at the next game start → `[config] migrated config.json at startup:
   MinMoveSpeedMultiplier: 0.3 → 0.7 …` then `rewrote config.json as format 3 …`, and the settings dump reads 0.7.
2. The feel: 70% run at empty; the curve (fast back into the fight, slow to top off) → Anton; `refill from empty to
   the peak line` avg ≥ 40.7 s, close to it when men walked.

## Step 16 — the guard really up + a step back that faces the enemy (DONE 2026-09-28; the research below was written before coding)

Anton asleep, no questions (the manager's brief). Why: BATTLE_PACING.md lever #2 and section B, BUGS
"defenceless", "backs turned", "empty AI attacks too fast". The 240v240 log: held men blocked 2%, stepping-back
men 5%, everyone else 33%; 80% of step backs had the back turned mid-step, 0.59 m of 2 m moved, 2 of 2159
arrived; at empty the step back fires every swing and a started step back dropped the hold (R1's rule), so the
empty band read a 2.2 s cycle against 8.5 s.

**Verified in the v1.4.8 source (`..\reference\game-decompiled\TaleWorlds.MountAndBlade\`)**
- `AgentComponent` (public abstract): `protected readonly Agent Agent`, ctor `(Agent)`, `public virtual void
  OnAIInputSet(ref Agent.EventControlFlag, ref Agent.MovementControlFlag, ref Vec2 inputVector)`; also OnTick,
  OnTickParallel, OnAgentRemoved, OnComponentRemoved, OnFormationSet… all empty virtuals.
- `Agent.OnAIInputSet` is an `[MBCallback(null, false)]` - the second argument is `isMultiThreadCallable`
  (MBCallback.cs): **false = the engine calls it from the main thread only**. It loops `foreach (AgentComponent c in
  _components) c.OnAIInputSet(ref …)` - so every component sees the input in the order it was ADDED, each one after
  the edits of the ones before (the last writer wins). `_components` is an `MBList` = a `List<T>`: never add or remove
  a component inside the callback (the foreach would throw).
- `Agent.SetHasOnAiInputSetCallback(bool)` / `GetHasOnAiInputSetCallback()` - public, native flag per agent. Nothing
  in the game turns it on (no vanilla component overrides OnAIInputSet) - without another mod it is off for everyone.
- `Agent.AddComponent` just appends (and sets CommonAIComponent / HumanAIComponent for those types); `Initialize()`
  is called only by `InitializeComponents` at the agent's build - a component added mid-battle is not initialized
  (ours needs nothing). `RemoveComponent` calls `OnComponentRemoved`. A removed agent keeps its list (its components
  get `OnAgentRemoved`).
- **Tick order** (Mission.cs): `OnPreTick` waits for the async agent tick, then behaviours' `OnPreMissionTick`; the
  native tick; `Mission.OnTick` runs every behaviour's `OnMissionTick` (our tick) and THEN starts
  `TickAgentsAndTeamsAsync` (the components' OnTickParallel / OnTick). So while our `OnMissionTick` runs nobody
  iterates an agent's component list: **adding a component from our tick is safe**.
- `Agent.MovementControlFlag`: Forward 1, Backward 2, StrafeRight 4, StrafeLeft 8, TurnRight 0x10, TurnLeft 0x20,
  AttackLeft 0x40 / Right 0x80 / Up 0x100 / Down 0x200 (`AttackMask` 0x3C0), DefendLeft 0x400 / Right 0x800 / Up 0x1000
  / Down 0x2000, **DefendAuto 0x4000** (`DefendMask` 0x7C00, `DefendDirMask` 0x3C00), DefendBlock 0x8000, Action
  0x10000. `EventControlFlag` (kick 0x8000, jump, wield, walk / run…) is separate - never touched.
- `inputVector` is the movement in the man's OWN frame: the player controller writes `MovementInputVector =
  (MovementAxisX, MovementAxisY)`, S = (0, −1) (MissionMainAgentController ~915). World ↔ local: `Agent.Frame`
  (native `GetRotationFrame` - the body frame) `.rotation.TransformToLocal(v)` = (s·v, f·v) (Mat3.cs) - RTS Camera
  does exactly this. `Agent.IsMainAgent` = `this == Mission.Current?.MainAgent` (managed); `IsAIControlled` reads the
  native controller pointer.
- Attack bits vanishing DURING a ready = the button let go = the RELEASE (step 13's finding for the player - the
  same input path for the AI). So a hold must never clear the bits of a man already in a ready; to stop a ready
  the way a player does, clear them AND press block - RTS Camera's `SetCancelAttack` = `&= ~AttackMask; |= DefendDown`.

**RTS Camera Command System 5.3.x** (`..\reference\RTSCamera.CommandSystem-decompiled\`, which Anton runs)
- `CommandSystemLogic.OnAgentCreated` → `agent.AddComponent(new CommandSystemAgentComponent(agent))` for EVERY agent;
  its `Initialize()` (called at the build) → `SetHasOnAiInputSetCallback(true)`: with RTS Camera every agent already
  has the callback on. It never turns it off (`UpdateHasOnAiInputSetCallback` only ever sets true).
- Its `OnAIInputSet` → `AgentAIInputHandler`: (1) **defensive hold** - only for `agent.IsAIControlled`, a formation in
  Circle / ShieldWall / Square and not charging: `SetAgentFlags(~AgentFlag.CanAttack)` and, with an enemy within 20 m,
  it rewrites `inputVector` to walk him back to his slot without turning; (2) **volley** (ranged men with RTS's volley
  mode on): sets / clears attack bits to draw and loose on command, `SetCancelAttack` to cancel.
- **Coexistence, decided**: ours is added LATER (lazily, mid-battle) than RTS Camera's (at creation), so ours runs
  after it and wins the frame. (1) Our step back refuses shield wall / square / circle (5d) - exactly the only
  arrangements RTS's defensive hold acts in - so the two never move the same man; our timer there only clears attack
  bits RTS has already disabled (CanAttack off) - both say "no attack". (2) A tired archer under RTS's volley: our
  timer clears the bits it sets to draw - he waits out his pause, as NoAttack made him wait before (the pause is the
  hard floor; RTS's own timers cancel the volley shot after 4-7.5 s). (3) A man the PLAYER commands directly (RTS
  lets you take any soldier): `IsMainAgent` / not AI-controlled - our callback returns untouched, the step back ends
  (`PlayerControl`), the hold ends (`MustEnd`), and nothing new starts on him. (4) **The callback flag**: we turn it on
  for a man when we hook him; we turn it off again only when he is idle AND it was off before we turned it on AND no
  other component on him overrides OnAIInputSet (checked by reflection, cached per type) - so RTS Camera's (or any
  mod's) callback is never switched off.
- Also seen: `AgentFlag.CanAttack` (RTS's defensive hold) - a third "no attack" lever. Not used (unknown guard
  behaviour, and it is an agent flag others write); noted as a fallback.

**The build (Claude's calls - Anton can overturn any)**
1. **One component per man, added lazily** (`AiInputComponent`, Missions/AiInputHook.cs) at his first Input hold or
   backpedal - from the tick, never inside a callback. At spawn would cost ~1000 objects and a callback for every
   fresh man; lazily only tired men who are really held get one. It holds a plain state object (`AiInputState` on
   `TrackedAgent`: hold / step-hold / backpedal flags + the local vector + counters) that the logic writes in its tick;
   the callback's early-out is one bool. It stays on the agent for his life (removing it buys nothing); the callback
   flag goes off when he is idle (the rule above).
2. **The AI timer by input** (`AttackRatePaceByInput`, on): instead of NoAttack, the callback clears ONLY the attack
   bits while the timer runs; defend bits, movement, the vector and every event flag stay his own. When he wanted to
   attack and holds no guard of his own, he raises one (`AiHoldRaiseGuard`, on - `DefendDown`, RTS Camera's proven
   cancel bit; with a shield it is the shield) - "waits guard up" made literal, and a ready that was somehow under way
   is CANCELLED instead of released (in a ready the guard bit is set whatever the switch says). No flag hygiene needed
   (nothing shared): no "waiting for a game job"; a man busy with a game job (object, ladder, detached, walking to an
   object) is still refused, as before, so A/B arms hold the same men.
3. **The step back by input** (`StepBackBackpedal`, on): the same roll, queue, cap, probe (the 5d safety gates: not
   the player, riders, shield wall / square / circle, retreat, routing, busy, the enemy within range, the spot on the
   navmesh, level, a straight clear way - for the WHOLE StepBackDistance) - then, instead of a scripted frame, the
   callback writes a backwards input: the fixed world direction straight away from his enemy at the start (the line
   the probe checked), turned into his own frame every tick from his current body frame (so he backs along the
   checked line whichever way the AI turns him - facing his enemy he simply walks backwards), at full stick (1.0 -
   like you holding S; the engine's backpedal speed and his tired run cap ride on it - plumbing, not a gameplay number).
   It ends at StepBackDistance covered along the line ("arrived"), at StepBackSeconds, on every 5d path, or when the
   ground 0.6 m further back stops being walkable (navmesh, height step, straight way - checked every 0.25 s:
   "edge ahead" - so a man backing along a wall walk or a ditch edge stops). Attacks held meanwhile
   (`StepBackHoldAttacks`) by the same bit rule. At its end we simply stop writing: his AI and formation take him back.
   RBM's short `SetTargetPosition` hops were the alternative: a native position lock that fights an active formation
   frame in a holding line (RBM uses it only in a charge, frame off) and still needs NoAttack for the attacks - the
   input route is cleaner and is what RTS Camera already does to walk men without turning them.
4. **The timer survives a step back**: R1's "a started step back drops the hold" is gone. Input timer: the hold starts
   whatever the step back does; one component clears the attack bits while EITHER runs, so attacks resume at max(timer
   end, step end). Legacy timer (NoAttack) under a SCRIPTED step (its frame owns the flag word): the hold is DEFERRED
   and set the tick the step back ends if time is left (else "covered by the step back"); under a backpedal it just
   starts. R1's original bug (a hold skipped for a step back that was then refused) cannot come back: the hold never
   looks at a PENDING step back and is never skipped for a running one - the smoke checks a refused, a started and a
   deferred case.
5. **A/B**: two booleans, both on - `AttackRatePaceByInput` (the timer) and `StepBackBackpedal` (the step back), plus
   `AiHoldRaiseGuard`; off = the step-13 / 5d techniques exactly (NoAttack, the scripted walk), with the new survival
   rule. Read at each START (a running hold / step finishes on the technique it began with); every switch logged.
   ModEnabled / AthleticsEnabled / AttackRatePaceHold / StepBackEnabled off release at once as before (the state
   cleared → the callback writes nothing from the next frame; the callback itself also checks ModEnabled and
   AthleticsEnabled first).
6. **Measured** (the summary): the GUARD by state (held by the timer / stepping back / everyone else, and "everyone
   else" split tired vs full strength); the input hook (men hooked, callback already on vs turned on by us, calls per
   second per hooked man, attack bits cleared, guards raised, own guards kept, readies cancelled, backpedal frames,
   holds during which the engine never called us - must be 0, component errors); facing every 0.25 s for every step
   back and "back turned at any sample"; arrived / edge ahead; holds that overlapped a step back and attacks that
   started before the later end; the technique in the summary header; the timer's floor D/m per band, and cycles
   with a step back inside now COUNT in the verdict (the timer survives them) and are shown apart.
7. **Expected, and why the verdict may still read "too fast"**: DESIGN's pause is D × (1/m − 1) - the attack part
   runs at × m, the AI's own gap after an attack runs INSIDE the timer. The verdict's target is the fresh cycle ÷ m
   (step 13 decision 12). NoAttack cost the AI a 1-3 s re-decision after it lifted, which happened to fill the gap;
   the input hook has no such latency. So the check that the SPEC holds is the timer rows (gap ≥ asked, 0 early) and
   the new "cycle vs the timer's floor D/m" ≥ ~100%; the verdict against fresh ÷ m is reported as before.

**Plumbing constants** (documented, not settings): backpedal input 1.0; the ground check 0.6 m further back every
0.25 s; the facing sample every 0.25 s; `DefendDown` as the raised guard; the callback-off rule; a hold or backpedal
of 0.3 s or more with no call from the engine = "never called".

**Built (DONE 2026-09-28)** - file map in CLAUDE.md "Layout"
- Settings 67 → 70: `AttackRatePaceByInput`, `AiHoldRaiseGuard` (Tired fighters, after `AttackRatePaceHold`),
  `StepBackBackpedal` (Tired fighters step back, after `StepBackEnabled`) - all true; schema, TraxSettings,
  defaults.json + refresh, DESIGN's table. No config format bump: a file without them takes the defaults (the
  "missing keys" path) - Anton's config.json gets them at his next start.
- Core `AiInput.cs` (AiInputMath: HoldAttacks, Backpedal - his Forward/Backward/Strafe bits out while backpedalling,
  since on foot the VECTOR moves a man and the player's controller never sets them - BackpedalVector, ToLocal,
  Covered, Arrived, Direction, LaterEnd, DeferredStillWorth; InputEdit), `AiHoldStats.cs` (the 4 "AI holds" lines);
  StepBack.cs (Backpedal, Arrived, EdgeAhead), StepBackStats (technique by counts, every-0.25 s facing, back turned
  at ANY sample, arrived, m/s, input releases), AttackRate.cs (PaceByInput, RaiseGuard, PaceTechnique;
  `PaceNotHeld.SteppingBack` → `CoveredByStepBack`), AttackRateStats (stepped-back cycles COUNTED and shown apart,
  the timer floor D/m, holds by technique, wording without "NoAttack" where both apply). Tests 351 → 367.
- Module `AiInputHook.cs` (AiInputState, AiInputComponent, AiInputHook), `AthleticsLogic.AiHolds.cs`;
  `InputPaceBody` (PaceBody.cs), `InputStepBackBody` + `IStepBackBody.Steer` (StepBackBody.cs); the logic picks the
  body by technique at each START (StepBackState / PaceState `.ByInput`); TickDeferred for NoAttack behind a
  scripted walk; OnMeleeHit → HoldHitTaken; the summary header's technique (`HoldsHeader`).
- Smoke 51 → 52 steps: `Program.AiInput.cs` (new); the old steps pinned to the old techniques in
  AthleticsDefaults / StepBackDefaults; R1's smoke rewritten (refused = held, started = deferred then set,
  covered = counted). deploy.ps1 green (build, AssemblyGuard, smoke, installed).

**Decisions made while building** (on top of the 7 above)
8. **One component, two wishes**: the timer's `HoldAttacks` and the step back's `StepHoldAttacks` are separate flags
   in one state - the attacks stay out while EITHER is on, so "the later end" needs no arithmetic at runtime
   (`AiInputMath.LaterEnd` documents it and the tests pin it).
9. **A NoAttack hold still WAITING for a game job keeps its technique** when the next hold starts on the same man
   (a switch to input meanwhile would otherwise orphan our NoAttack flag - a man who never attacks again).
10. **In a ready the guard is ALWAYS pressed** (whatever `AiHoldRaiseGuard` says): clearing the bits alone would
    release the blow. `AiHoldRaiseGuard` off only changes the case "he wants to START an attack".
11. **The step back's `StepBackHoldAttacks` is taken at its start** (as 5d's was for the running scripted step).
12. **The callback flag** is turned on in the tick when a man is hooked and off when both his wishes are gone,
    only if we turned it on and no other component overrides `OnAIInputSet` (reflection, cached per type) - with
    RTS Camera it is already on for everyone and never touched.
13. **The summary header** carries the technique ("AI holds: Input / Legacy / the timer X, the step back Y /
    mixed") from what the battle USED (else the settings at the end).
14. **The verdict's population changed**: cycles with a step back inside count now (they did not since 5e) - the
    timer survives them, so they are the attack rhythm; the band line shows both halves.

**UNVERIFIED - only the game can tell (PLAYTEST D1, D4, L5, L6, L6b; the line that settles each)**
1. The engine calls `OnAIInputSet` for a hooked man, often enough → L6b `the input hook: … calls while held N (about
   R a second per held man)` with R well above 0, `holds / backpedals the engine never called us during 0 / 0`, no
   `[rate] WARNING: … never called our input hook`.
2. Clearing the attack bits stops the AI's attacks → `attack rate - AI timer ends: … an attack started anyway K`
   and L6b `AI attacks that started while a hold or a step back held him anyway K`, both about 0.
3. **THE fix: held men keep their guard** → L6b `GUARD: held by the timer X%, stepping back Y% … everyone else Z%`,
   gaps within ~10 points (before: 2% / 5% / 33%). If X stays low: the D4 A/B with *Held AI raise their guard* off
   tells whether DefendDown helps or hurts; then `AgentFlag.CanAttack` (RTS Camera's lever) is the next try.
4. **The backpedal faces the enemy** → L6 `step backs with the back turned at ANY sample K of M (P%)` under ~5%,
   mid-step `back turned` about 0 (before ~80%).
5. The backpedal really moves him (speed, distance) → L6 `moves: avg X m of 2.00 …- about V m/s` and `arrived`;
   the first step back's every-0.25 s samples. If V is tiny, the engine ignores a vector against its own
   movement (then: RBM's `SetTargetPosition` hops).
6. The edge check keeps men on walls and out of ditches → sieges: `the ground ends behind him (edge ahead) N`
   and Anton's eyes (PLAYTEST F2).
7. The pause survives a step back → L5 each timer row's `the timer's floor D/m … the cycle vs it: P%` ≥ ~100% (the
   empty band read ~50% before), L6b `holds that overlapped a step back N` > 0; the verdict line (fresh ÷ m) may
   still read "too fast" (decision 7).
8. The AI re-decides at once after an input hold (no NoAttack latency) → L5 `the next ready came avg T s after a
   hold ended` well under the old 1.5-2.6 s.
9. RTS Camera beside it (Anton runs it) → L6b `the callback already on for N of them` = all hooked men; volley /
   defensive hold unaffected (his eyes); `the player or a non-AI agent passed untouched` counts the men he took over.
10. Cost → `Athletics tick cost` against a step-15 log of the same size.

## Step 17 — review (DONE 2026-09-28)

The second fresh-eyes review, over steps 12-16. The findings are `docs/REVIEW.md` R25-R36 (0 blockers, 0 majors,
4 minors - 3 fixed, 1 deferred - 1 For Anton, 8 not a bug). The lessons worth keeping:

- **"The player" is two facts: Mission.MainAgent AND your hands on him.** RTS Camera's free camera sets
  `MainAgent.Controller = AI` (its `Utility.AIControlMainAgent`) - the agent is still `IsMainAgent`, but the
  controller writes no input. Anything that acts through the player's INPUT must also ask `!IsAIControlled`
  (a pointer read, safe in a hit callback) - else it starts states it cannot enforce, and the checks that prove
  it works turn into false alarms (R25). Everything that acts on the AI's input refuses `IsMainAgent`, so an
  AI-driven hero falls between the two (For Anton).
- **Attack bits are not only attacks.** `RangedSiegeWeapon.OnTick` fires on its pilot's `MovementFlags &
  AttackMask` (R26). Before clearing or rewriting any input bit, grep the decompile for every READER of that bit
  (`MovementControlFlag.Attack`, `AttackMask`, the raw 960 / 0x3C0) - one grep found the only other reader.
- **A release that "does nothing in the engine" is still a release of OUR state.** "Handed over to the game's job =
  never release" was written for the scripted walk (its frame is the game's now); the backpedal inherited the rule,
  but its release only stops our own input and our own callback flag, so skipping it leaked the flag (R27). When a
  new technique reuses an old end rule, re-ask what each technique's release actually touches.
- **A "must be 0" count needs its denominator next to it.** A single-event warning ("the engine never called our
  hook") can be a stunned man, not a dead hook; the reading rule is the ratio against all holds plus the calls per
  second (R28, PLAYTEST L6b). Write new diagnostics as "N of M" from the start.
- **Mutation-check every smoke check you add.** Reverting the three fixes made 11 checks fail (and reproduced R25's
  false alarm as `started anyway 2, early 2`) - that is what makes the check worth keeping. A new smoke section can
  also shift later sections' state (here: extra swings drained the stand-in player into another f band) - reset what
  you spent (`ResetFull`) before handing over.
- **Checked and fine, so nobody re-derives them** (REVIEW R29-R36): the player's hold cannot outlive its attack; the
  gate's move to index 0 is safe (`OnMissionBehaviorInitialize` runs while `AfterStart` enumerates SUBMODULES; the
  only writer of `MovementFlags` is `MissionMainAgentController`); the component is added only between
  `OnMissionTick` and the async agent tick; `DefendDown` is only ever a replaced attack wish; the backpedal ends on
  `StepBackSeconds` whatever else happens; R1 stays closed; the refill curve's closed forms re-derived; no per-tick
  allocation in the new paths.

## Step 18 — kicks and shield bashes cost Athletics (DONE 2026-09-28)

Anton's call (2026-09-28): 3 points, a slider, hero / leader multipliers like any blow. `CostPerKickOrBash` (0-20,
Athletics group, after `CostPerBlow`); DESIGN §2 "Kicks and shield bashes cost too", interpretations 8 + 19.

- **Where a kick shows is NOT proven.** Step 5 polled channel 1 only; Anton's 2026-09-27 log saw one shield bash there
  (`shield bashes 1, kick/bash hits 1`) and NO kick in any battle (K = 0, H = 0 - the AI rarely kicks). The game's own
  `StandingPoint.TickAux` reads Jump / **Kick** / WeaponBash on **channel 0** (`GetCurrentAction(0)`), `Agent.HandleDropWeapon`
  reads WeaponBash on channel 1. So the tick now also polls channel 0 **on foot** (one more native call per fighter;
  step 17's 480-man log measured the whole poll at 0.077 ms a tick) and the summary counts each kick / bash by channel.
  **After the playtest: if kicks show on one channel only, drop the other read** (`seen starting: kicks K (channel 1 a,
  channel 0 b)`). Action types (Native `action_types.xml`): `act_kick_*` = Kick 28, `*_continue*` = KickContinue 29,
  `*_hit*` = KickHit 30 (all "a kick" - one action), every bash (`act_shield_bash`, `act_hand_shield_bash`, `act_staff_bash`,
  `act_2h_bash`, left-stance too) = WeaponBash 31; `act_hit_*_bash` (the blocked bash) has no type (Other).
- **One decision per action** (Core `KickBashTracker`): channels OR-ed (a kick on both = one kick; it ends when neither
  shows it); a kick → bash on the same channel is a new one; the hit (`IsAlternativeAttack`) first reads BOTH channels
  (like a swing's hit), then charges only if neither shows a kick / bash and none was decided within 1.0 s
  (`SameActionSeconds`, plumbing) - the poll's later sight of that same action is then not charged. So a bash that lands
  is charged once, and a kick the poll never sees is still paid by its hit (counted: `at their hit with no kick or bash seen`).
- **Charged at the START, landed or not** - `CostOnMiss` stays a blow rule (Claude's call, DESIGN 19). A charge > 0 restarts
  the refill delay (effort) and follows every curve; a cost of 0 is nothing at all (no delay restart) and is counted `free`.
  `Fighter.KicksAndBashes` counts them; `Blows` never does (the YOU lines' "after N blows" and DESIGN's blow counts stay true).
- **Never a timer**: `ChargeKickOrBash` shares `AfterCharge` (retarget, peak line, exhaustion) with `Charge` but nothing of the
  attack rate - kicks / bashes were never phases (`PhaseOfAction`), never `StartRelease`, never `PlayerAttackStarting`. Step
  13's input rules are untouched: the kick flag is never cleared, a bash still waits while your pause runs (attack bits).
- **Riders**: never kick (no channel-0 read mounted); a mounted WeaponBash, if the engine ever plays one (vanilla has none,
  as far as the data shows), is charged like any bash and counted `by riders`.
- Log: `[athletics] YOU: kick / shield bash at … s cost 1.69 Athletics (3.00 x0.56 hero party leader): …` (always, bucket
  `athletics-player-kick`; the first per battle with the rule), `~[athletics] kick (on foot): …` (verbose, its own bucket
  `athletics-kick` so blow lines cannot starve it). Summary: the blows line adds `+ kicks/bashes N (not blows …)` and the
  spent points' share; `Athletics kicks/bashes charged N (P points; by riders R): …` replaced `Athletics free …`'s kick part.
- Tests 376 (+9, `KickBashTests`); smoke: a new step (costs, dedupe on every path, no timer / roll, 0 / off live, the YOU
  line, Core's action codes = the game's) + the master switch step (a kick while off costs nothing).

## Step 19 — the hideout boss fight is a fresh start for the player's side (DONE 2026-09-28; the research below was written before coding)

Anton (2026-09-28): "when I'm clearing a hideout, when the cutscene where the boss comes with his few friends, our Athletics is
regenerated - either if I chose to duel him or to fight men to men - because they will come fresh and we will be tired."

**How v1.4.8 runs a hideout's boss phase** (decompiled SandBox; no Harmony needed)
- **Two hideout missions, one boss phase.** `SandBoxMissions.OpenHideoutBattleMission` ("HideoutBattle": `HideoutMissionController`
  + `HideoutCinematicController`) and `OpenHideoutAmbushMission` ("HideoutAmbushMission", the stealth version: `HideoutAmbushMissionController`
  + `HideoutAmbushBossFightCinematicController`). Both carry a `MissionObjectiveLogic` (TaleWorlds.MountAndBlade, public) and run the
  same private state machine: … → the first fight → `CutSceneBeforeBossFight` → `ConversationBetweenLeaders` → `BossFightWithDuel` |
  `BossFightWithAll` (a private enum field - `_hideoutMissionState` / `_currentHideoutMissionState`; not read).
- **The boss phase starts** when the bandits' side is depleted (`IsSideDepleted`: NO active bandit left - so no first-phase bandit
  survives into it): mode `CutScene` (9) for 4 s (`FirstPhaseEndInSeconds`), then the cinematic. Its `OnInitialFadeOutOver` callback
  **spawns the boss and his men** (`SpawnBossAndBodyguards` → `SpawnRemainingTroopsForBossFight` → `Mission.SpawnTroop`, tag
  `_hideout_bandit`; the classic spawns the troops not supplied yet, the ambush `Clamp(population / 2, 4, 20)`), sets the two teams
  not-enemies and places everyone. `OnCutSceneOver` restores the mode (classic: the one before; ambush: Battle) and starts the
  conversation with the boss.
- **So the boss's side is FRESH by construction for us**: new agents → `OnAgentBuild` → `Track` → a full bar. Nothing to refill there
  (Anton's call: only the player's side). The refill line still measures them ("the boss's side: N, all at full") to prove it in game.
- **The choice** (`HideoutConversationsCampaignBehavior`): "Very well." → `ConversationManager.ConversationEndOneShot +=
  StartBossFightDuelMode`; "I don't fight duels with brigands." → `+= StartBossFightBattleMode` (the static of whichever controller the
  mission has). At the conversation's end, synchronously:
  - **Duel** (`StartBossFightDuelModeInternal`): teams enemies again; every AI human of the player's team except you →
    `SetTeam(Team.Invalid)`, a scripted position where he stands, looking at you (**your men sit out**); the boss's men the same, looking
    at the boss; the boss alarmed. Then `new DefeatHideoutBossObjective(mission, isDuel: true)` → `MissionObjectiveLogic.StartObjective`.
  - **Battle** (`StartBossFightBattleModeInternal`): teams enemies, everyone alarmed, the player's formations Charge (ambush: both
    sides' order 4 = Charge). Then `new DefeatHideoutBossObjective(mission, isDuel: false)` → `StartObjective`.
- **THE HOOK - public, polled, one reference compare a tick**: `Mission.GetMissionBehavior<MissionObjectiveLogic>().CurrentObjective`
  (public getter) becomes an objective whose `UniqueId` (public abstract, a constant string) is
  **`"hideout_mission_defeat_hideout_boss_objective"`** - in both missions, for both choices, the same call that set the teams (so by
  our next tick the duel's teams are already in place). Nothing else starts that objective. `StartObjective` completes the previous one
  first, and the duel's / fight's end completes this one (`CurrentObjective` → null) - so it is seen exactly once.
- **Duel or battle - from the objective itself**: `Name` is a `TextObject` built from `"{=QEynMlwL}Win the Duel"` (duel) or
  `"{=0sPTRh6L}Win the Fight"` (battle); `TextObject.Value` is a PUBLIC FIELD holding that raw string (not localized; `GetID()` would
  allocate). The ids decide; the English words are a fallback; neither → "unknown" (the refill does not need it - who refills comes
  from the teams).
- **Who is "the player's side" at that moment**: `agent.Team.IsPlayerAlly` (Team.Side == PlayerTeam.Side - the player's team and an
  allied one). In the duel your men are on `Team.Invalid` (side None, no mission → not a player ally) → only YOU refill, exactly as
  Anton wants ("his men sit out"). The boss's side = a real team that is not the player's; the duel's onlookers (both sides' men) =
  `Team.Invalid` - "standing aside", not refilled.
- **The intro, for the summary's "did the hook fire?"**: `Mission.Mode == MissionMode.CutScene` (9) in a hideout mission (a behaviour
  named `HideoutMissionController` / `HideoutAmbushMissionController` - by NAME, the module does not reference SandBox) = the boss phase
  began. Intro seen + objective never seen = the hook never fired (or the player left during the talk) → the summary says "tell Claude".
  Both cinematic controllers also have a public `IsCinematicActive`, but reading it would need SandBox types - the mode is enough.
- **Timing**: the 4 s wait + the ~8 s cinematic + the conversation all run in mission time, so any pause, hold or step back that was
  running when the last first-phase bandit fell has long run out by the fight's start; the refill still releases whatever runs (a
  mod could pause time, a future version could shorten the intro) - cheap and safe.

**The plan (built as below unless the code says otherwise)**
- Core: `AthleticsMath.FreshStart(fighter, rules, health)` - the refill to the top he can refill to (full, or the health left under
  the cap, the health recorded first), not exhausted, no refill run, no regen delay; `HideoutBossFight.cs` - the objective id, the
  text ids → `BossFightKind` (Duel / Battle / Unknown), the controller names, `HideoutSide` (Player / Boss / Aside / Gone), the gate
  (`RefillOffBecause` - ModEnabled, AthleticsEnabled, HideoutBossFightRefill), `HideoutBossFightStats` (the facts, the `[athletics]`
  line, the `[summary]` lines). New end reasons `FreshStart` for the step back, the AI timer and your timer (named in the summaries
  only when > 0, so the pinned lines stay).
- Module: `AthleticsLogic.Hideout.cs` - `NoteHideoutMission` at the first tick (the controller by name, the objective logic),
  `TickHideout` at the top of the Athletics tick (the intro by mode, the objective by reference), `ObserveObjective`, `BossFightBegan`
  (collect the sides - engine reads - THEN refill; an exception before the refill = nothing refilled; releases each on its own path:
  `Finish(FreshStart)`, `EndHold(FreshStart)` + a queued / deferred pause dropped, `ReleasePlayerTimer(FreshStart)`, the phases reset,
  the speeds re-targeted). A seam `HideoutSideOf` (the smoke's stand-in for the teams, like `StepBackBody`).
- Setting `HideoutBossFightRefill` (bool, on, Refill group, live).

**Built as planned** (file map in CLAUDE.md "Layout"). Decisions and gotchas worth keeping:
- **The refill goes to the wound cap AT ONCE** (`FreshStart` records today's health first - `HealthOf`, a killing blow in
  flight keeps the last known health), unlike the master switch's "back on" (`ResetFull`, capped at the next regen step).
  The peak line stays on the full pool, so a man at 60% health refills to 0.6 = f 0.8, not full strength.
- **Order in `BossFightBegan`**: `BeginFight` (once per mission - a second boss objective is counted "came back", never
  refilled) → the gate (`RefillOffBecause`: ModEnabled, AthleticsEnabled, HideoutBossFightRefill - the line is written
  either way) → pass 1, the side reads (every engine read; an exception = `[error] hideout.refill` once per mission site,
  `FailedAt`, NOTHING refilled) → pass 2 per man: `FreshStart`, the releases, `ResetPhases` (no cycle or timer gap spans
  the fresh start), `RetargetSpeed(exact)` → the line. Called from the top of `TickAthletics` (after `TrackPlayer`), so
  this tick's poll applies the speeds within its budget and this tick's step-back / pause passes see the ends.
- **Releases, each on its existing path**: a running step back `Finish(FreshStart, native)` (the backpedal's wish off too); a
  queued one out of `_stepPending` + `Refuse(NoLongerEligible)` (keeps "rolled yes = started + refused"); a running AI
  pause `EndHold(FreshStart)` (NoAttack lifted or the input wish off); a queued one `Pending = false` (the queue skips it);
  a deferred one out of `_paceDeferred`; your pause `ReleasePlayerTimer(FreshStart)` ("a fresh start (the hideout boss
  fight began): attack at once"). New enum values `StepBackEnd.FreshStart`, `PaceEnd.FreshStart` (7, Count 8),
  `PlayerTimerEnd.FreshStart` (5, Count 6); the attack-rate summary names them only when > 0 (pinned lines unchanged); the
  step-back "cut short" list names any non-zero reason by itself.
- **The boss's side is measured, never touched**: `AddBossSide(fraction, usable top)` → "all fresh (at full - spawned for
  this fight)" or "N at full, lowest X% - NOT all fresh (tell Claude)". The research says they spawn fresh; the line is the
  in-game proof Anton asked for.
- **The seam**: `HideoutSideOf` (null in game = `SideInGame`: removed / not active → Gone; no team, side None or an invalid
  team → Aside; `Team.IsPlayerAlly` → Player; else Boss). The smoke also hands `ObserveObjective` the game's OWN
  `MissionObjective` type (a subclass with the boss id and a real `TextObject`) - TextObject and MissionObjective construct
  offline (managed only); the smoke project now references TaleWorlds.Localization.
- **Mutation-checked** (step 17's lesson): no releases + no cap → 13 failed smoke checks, all in the new step.
- Tests 387 (+11, `HideoutBossFightTests`); smoke +1 step (Program.Hideout.cs) + the master switch step (mod off = nobody
  refilled, the line still written). 72 settings.

**UNVERIFIED — only the game can tell (PLAYTEST F5; the line that settles each)**
1. `MissionObjectiveLogic.CurrentObjective` shows the boss objective in both hideout missions → `[athletics] hideout boss
   fight (duel|battle): refilled …`; the summary's `NEVER SEEN` must not appear after a boss fight.
2. The text ids tell the choice → `(duel)` / `(battle)`, never `(duel or battle)`.
3. The duel's teams: your men on Team.Invalid → `refilled 1 of the player's side` and `standing aside: N`.
4. The boss's side fresh → `the boss's side: …, all fresh (at full - spawned for this fight)`.
5. The intro seen by the mode → `[athletics] hideout: the boss intro began at …` (the summary's "the boss intro at …").
6. The bar jumps on screen the same frame (the HUD reads every refresh) - Anton's eye.

## Step 20 — hold ALT: Athletics + health under vanilla's formation markers (DONE 2026-09-28; the research below was written before coding)

Anton (2026-09-28): "Can you make it so I see the Athletics and health numbers above the troops when I hold ALT - it now shows me
the troop count and distance?" = LATER #8 (squad bars, AI_NOTES "Step 8") in its "only while vanilla shows the markers" form.

**Vanilla's formation markers in v1.4.8** (decompiled; `..\reference\game-decompiled\`)
- **Who draws them**: `MissionGauntletFormationMarker : MissionBattleUIBaseView` (TaleWorlds.MountAndBlade.GauntletUI, overrides
  `MissionFormationMarkerUIHandler`), in every battle-type mission (field battle, siege + lord's hall, hideout + ambush, alley fight,
  combat-with-dialogue, custom battle, War Sails' naval battles - `ViewCreator.CreateMissionFormationMarkerUIHandler`). It builds ONE
  layer `new GauntletLayer("MissionFormationMarker", ViewOrderPriority++)` and loads movie **`FormationMarker`**
  (Modules\Native\GUI\Prefabs\Mission\FormationMarker.xml) over a `MissionFormationMarkerVM` (ViewModelCollection, public). The view
  exists only while the game's Hide battle UI is off (MissionBattleUIBaseView creates / destroys it on that switch); photo mode sets
  its layer's ContextAlpha 0.
- **WHEN they show** (`OnMissionScreenTick`): `IsEnabled = Input.IsGameKeyDown(5) || Mission.IsOrderMenuOpen`, set every frame EXCEPT
  in Deployment mode (6), where it keeps its last value. Game key 5 = **`ShowIndicators`** (GenericGameKeyContext: Left Alt, the
  controller's LB; rebindable). So: ALT held OR the orders menu open. No setting, no always-on option. While enabled, every frame:
  `RefreshFormationMarkers` (adds / removes targets, sorts them FAR FIRST, `Size = CountOfUnits`), the targeting highlights, the
  positions. After a release the positions keep updating for 2 s (a fade timer) while each marker's alpha lerps to 0 (dt × 12: gone in
  ~0.25 s).
- **Which formations**: EVERY team's formations with men (`Mission.Teams → FormationsIncludingEmpty, CountOfUnits > 0`) - yours
  (`TeamType` 0, "Player" state), an ally's (1), the ENEMY's (2) - so vanilla marks the enemy too. The count includes the player.
- **Where** (`UpdateMarkerPositions`): `MBWindowManager.WorldToScreen(MissionScreen.CombatCamera, Formation.CachedMedianPosition
  .GetGroundVec3() + (0, 0, 3))` → `ScreenPosition` (screen PIXELS), `WSign` = sign of w (−1 behind the camera), `Distance` = camera →
  the median's ground; an invalid median → (−10000, −10000), WSign −1. The distance TEXT is shown only with the game option "Show
  formation distances" (ManagedOptions 14; Anton has it on): from the player's agent, or the camera without one.
- **The widget** (prefab): each target is a `FormationMarkerListPanel` (TaleWorlds.MountAndBlade.GauntletUI.Widgets, public; a
  VerticalTopToBottom ListPanel, CoverChildren) bound `Position="@ScreenPosition"`, holding top to bottom: the count (`IntText=@Size`,
  brush NameMarker.Distance.Text), the 50 × 50 team/type icon (× a distance scale 0.5-1.4 - exactly 1 while distances are shown),
  the distance row (footprint 22 × 36 + the number, 60 wide, only with distances). Its `OnLateUpdate` CENTRES it on the point:
  `ScaledPositionXOffset = Position.X − Size.X / 2`, `…Y − Size.Y / 2` (pixels); visible while `WSign > 0` (its "partly on screen" test
  is always true in front of the camera); `IsTargetingAFormation` (the orders menu open and the enemy formation targeted by a selected
  formation's charge/advance) and not fully on screen → PINNED at the screen's edge. Alpha by distance (the prefab's FarAlphaTarget 0.7,
  FarDistanceCutoff 500, CloseDistanceCutoff 10, ClosestFadeoutRange 5): 0.7 beyond 500 m, 1 at 10 m, 0 under 5 m (alpha ≤ 0.05 →
  IsVisible false).
- **Can we read them live, as step 9 read the cards? YES - and better: the data behind them is public.** `MissionScreen.FindLayer<
  GauntletLayer>("MissionFormationMarker")` → `layer.GetMovieIdentifier("FormationMarker")` (public) → `.DataSource` IS the
  `MissionFormationMarkerVM`: its public `IsEnabled` is vanilla's own "markers shown" (the exact mirror, incl. the Deployment freeze) and
  `Targets[i]` (public) carry `Formation` (the formation itself - no matching by counts), `ScreenPosition`, `WSign`, `Distance`,
  `TeamType`, `Size`. The movie is NOT a generated one (not in AutoGenerated0/1's class lists), so the widgets are real
  `FormationMarkerListPanel`s under the `{Targets}` widget: their public `Size` (pixels), `AlphaFactor`, `IsVisible`,
  `IsTargetingAFormation`, `ScaledPositionXOffset/YOffset` and the bound `Position`. `GauntletView.OnListSorted` re-orders the item
  widgets to the list's order (SetSiblingIndex), so child j = `Targets[j]`, confirmed by `Position == ScreenPosition` (the binding
  copies the value). Nothing patched, nothing reflected.
- **Same-frame alignment**: `MissionViewsContainer` ticks views in insertion order; ours is added on the Athletics logic's first tick,
  after vanilla's, so the `ScreenPosition` we read is the one vanilla computed THIS frame. Vanilla's panel moves itself in its layer's
  LateUpdate and `UIContext.LateUpdate` re-lays out the same frame (CalculateCanvas → LateUpdate → RecalculateCanvas) - the marker is
  drawn at this frame's point; our label (bound in our tick) is laid out in our layer's update the same frame. So the label goes to
  `(ScreenPosition.X, ScreenPosition.Y + Size.Y / 2)` - the marker's bottom centre, computed exactly as vanilla centres it - and never
  lags. (Reading `GlobalPosition` instead would be one frame late.) A brand-new marker widget (the battle's first ALT press, a new
  formation) has size 0 until its first layout: hidden for that frame.
- **RTS Camera 5.3.38** (Anton runs it; the main mod decompiled into `..\reference\RTSCamera-decompiled\`, the Command System is in
  `..\reference\RTSCamera.CommandSystem-decompiled\`): four Harmony patches on the markers, none on WHEN they show, the layer, the movie
  or the projection - `Patch_MissionFormationMarkerVM` (replaces RefreshFormationMarkers: the player's own formation's marker is hidden
  while he is alone in it, outside the free camera), `Patch_FormationMarkerListPanel` (close markers keep alpha 0.2 instead of 0 while
  you do not control your agent - the free camera), `Patch.Fix.Patch_MissionGauntletFormationMarker` (free camera: the distance text =
  the camera's), and the Command System's `RefreshTargetProperties` (the targeting highlight incl. Advance / ChargeWithTarget). Its
  free camera moves `CombatCamera` itself, which vanilla projects from. Reading the live VM + widgets follows all of it for free. Its
  CommandSystemLogic reads the same ShowIndicators key (outline colours) - no clash.

**The plan**
- **Core `AltMarkers.cs`** (pure, tested): `AltMarker` (one marker as read: team type, formation index, key, the point, WSign,
  distance, men, the widget's size / alpha / pinned offsets, the stats), `AltMarkerFrame` (≤ 64, reused), `AltMarkerMath` - `Pair`
  (widgets ↔ targets: index first, then by the exact point), `Sight` (why a label shows or not: enemy switched off, no stats, behind
  the camera, faded / too close, not laid out, pinned, shown), `Place` (the box's left / top in pixels: the marker's bottom centre +
  the gap × UI scale), `VanillaAlpha(distance)` (the prefab's cut-offs - the projection fallback's "too close"), the texts (the strip's
  `72% ± 8` / `HP 81%`), `AltMarkerLayout`, `AltMarkerStats` + the [summary] line.
- **Read API**: formation stats for EVERY team (≤ 8 teams × 10 formations by `Team.TeamIndex`, allocation-free, same pass every
  FormationStatsRefreshSeconds); the player still left out of his own.
- **Module**: `IFormationMarkerSource` / `GauntletFormationMarkers` (the live read above) + `ProjectedFormationMarkers` (the fallback:
  the same world point projected ourselves, vanilla's condition copied - ALT via HudFrame's new `ShowIndicatorsKey`, or the orders menu;
  a nominal marker height), `AltMarkerView : TraxHudView` (condition = vanilla's `IsEnabled`, else the copied rule; NeedsPlayer false -
  vanilla shows markers with the player down; QuietConditionToggles), `AltMarkersVM` (a grow-only list of labels keyed by team +
  formation, so a label never swaps formations when vanilla re-sorts), prefab `TraxAltMarkers.xml`: under each marker, centred,
  "72% ± 8" in the colour of the men's f and "HP 81%", then an optional slim bar like the strip's.
- **Settings**: `ShowAltMarkerStats` (on), `AltMarkersShowEnemy` (on) in a new group "Formation markers (hold ALT)"; Advanced
  `AltMarkerTextSize` 13, `AltMarkerOffset` 2 (gap under the marker, UI px), `AltMarkerBarWidth` 60, `AltMarkerBarHeight` 3 (0 = no
  bar). Allies' formations count with yours (always); the enemy's behind the switch. `ShowFormationBars` is built as
  `ShowAltMarkerStats`; `FormationBarsAlways` / `FormationBarHeight` stay planned (an always-on form would need our own projection
  always; the height is vanilla's 3 m).

**Built (DONE 2026-09-28) - as planned, file map in CLAUDE.md "Layout".** Decisions and gotchas worth keeping:
- **The condition is vanilla's own flag** (`MissionFormationMarkerVM.IsEnabled`), read every frame in `ViewConditionMet` - so the
  labels show with the orders menu too (DESIGN interpretation 21), follow any mod that changes the rule, and stay down in
  Deployment. When it cannot be read (no layer - e.g. Hide battle UI just rebuilt it, or another mod replaced the markers), the
  rule is copied: HudFrame's new `ShowIndicatorsKey` (game key 5, read only for a view with `ReadsIndicatorKey`) or the orders
  menu. `NeedsPlayer` false - vanilla shows its markers with the player down.
- **Placement = vanilla's own centring, the same frame**: the label's box (a 200-UI-px centring box, `ScaledSuggestedWidth` in
  pixels) goes to `(ScreenPosition.X, ScreenPosition.Y + widget height / 2 + AltMarkerOffset × scale)`. Pinned markers (a targeted
  enemy off-screen, orders menu open) use the widget's own `ScaledPositionX/YOffset` (last frame's - it is pinned, so it barely
  moves). The widget's `AlphaFactor` ≤ 0.05 = faded (under 5 m, or the fade after a release; RTS Camera's free camera keeps 0.2 -
  followed). The first frame of each ALT press reads the fade-out's ~0 alpha → no label for ONE frame (vanilla sets the alpha in
  its LateUpdate, after our tick) - accepted, invisible.
- **Labels are keyed, grow-only** (`AltMarkersVM.LabelFor(key)`, key = TeamIndex × 16 + formation): vanilla re-sorts its targets
  far-first EVERY frame; a list re-bound by index would rebuild texts (allocations) and could flicker. A label with no marker this
  frame is only hidden. The list lives for one show (a new VM per layer build, like the strip).
- **Values** are pushed when `FormationStatsVersion` or the settings version moves, or a label is new - `PushedStats` /
  `PushedSettings` per label (int compares per frame). Texts rebuild only on a changed number (the strip's `SetValues` rule).
- **The read API covers every team now**: slots `TeamIndex × 10 + formation` (`MaxStatTeams` 8), one pass over all tracked fighters
  every FormationStatsRefreshSeconds. **Gotcha**: the loop reads the managed `Formation` / `Team` first and `IsActive()` last -
  `Agent.State` reads the engine's state pointer, and the smoke's uninitialized agents would crash on it (the old code only reached
  it for the player's team). A team index past 7 is counted and named once at the battle's end ("tell Claude").
- **Text**: vanilla's marker brush `NameMarker.Distance.Text` (the count's and distance's font, outline 0.5 - reads over the
  battlefield), size 16 by default (vanilla's count is 22; 13 was too small beside it), the Athletics number coloured by the men's f.
- **Fallbacks, per show** (a new ALT press tries the live markers again): a marker whose widget is not at its point → a nominal size
  for it (60 × 108 UI px, 72 without the distance row); no widgets → the game's points, nominal sizes; no layer / movie / marker
  VM → `ProjectedFormationMarkers` for the rest of the show. The first of each kind per mission in full, the rest verbose; the
  fallback text is built only on the show's first such frame (no per-frame allocation).
- **Smoke**: `[error]` lines share a per-site burst of 3 (TraxLog's ErrorLimiter) - the earlier fail-safe steps spend "hud.tick", so
  the ALT fail-safe proves the error by the count and the DISABLED line.
- Tests 401 (+14, `AltMarkerTests`); smoke 55 steps (+3: the prefab, the view, the fail safe; + the master-switch step); 78 settings
  in 10 groups.

**UNVERIFIED — only the game can tell (PLAYTEST E4; the line that settles each)**
1. The marker layer and its VM are found and read live → `[hud] ALT markers: first shown at … - technique: the game's own formation
   markers read live … (layer MissionFormationMarker, movie FormationMarker: N markers, N marker widgets)` - the same count twice,
   no `FALLBACK` / `a marker without its widget` line; the summary's technique `read live` with no `(shows: …)` mix.
2. The labels sit under the markers at Anton's resolution / UI scale and follow the camera without lag (the same-frame reasoning
   above) → Anton's eye; the first-show line's label centre x = marker x, top y = marker y + half its height + 2 × scale.
3. The widget's `Size` is the whole marker column (count + icon + distance) → the label clears the distance number (eye); if it
   overlaps, `AltMarkerOffset` moves it live - Anton reports the number.
4. Enemy formations get stats → `values at …: … | enemy 1 Infantry 64% ± 12 HP 95% (…)`; the summary's `enemy N` > 0.
5. RTS Camera's free camera: labels under the markers wherever the camera flies; the solo-formation marker hidden → no label (eye).
6. The one-frame gap at each ALT press is invisible (eye); the fade after a release (vanilla ~0.25 s) vs ours (at once) looks fine.

## Step 20b — Anton's tuned defaults: run floor 0.6, the swing animation a straight line to 85% (DONE 2026-09-28)

Anton after his playtest (2026-09-28, via the manager): "speed (run) floor sweetspot is 60% when their athletics is at 0%"
and "swing speed does get reduced but to 85%, so swings do show as slower, but not as dramatically as our original 20% (the
action itself, the delay in seconds is nice, leave it be like it is)".

**Built**
- `defaults.json`: `MinMoveSpeedMultiplier` 0.7 → **0.6**, `AttackAnimationMinPercent` 100 → **85**. DESIGN's Default column
  keeps the INITIAL 0.7 / 100 (CLAUDE.md: a tuning, not a design change - unlike steps 13 / 14, which moved DESIGN's value
  with a rule change); the unit tests keep running on them.
- **The animation RULE** (Core `AttackTimerMath`): `AnimationMultiplier(f, A)` = A + (1 − A) × f (`AthleticsMath.Curve`), A =
  AttackAnimationMinPercent / 100 - full speed at and above the peak line, 0.925 halfway, 0.85 empty. Steps 13-20 used
  max(m, A): with 85 that sits at 0.85 from f ≈ 0.81 down (m = 0.2 + 0.8 f). 100 = 1 always (step 13's PAUSE ONLY, one slider
  away).
- **f off the APPLIED m** (`PeakShareOfAttack(m, S)` = (m − S) / (1 − S) clamped, `AnimationForAttack`): the stat decorator
  only knows the m it applies (`SpeedFactorsFor` → `st.SpeedMultiplier`, stepped by 0.05). Reading f off it keeps every
  property recompute - ours and the game's own (weapon switch, mount) - on the same value, needs no new state or recompute
  trigger, and the line moves in m's steps (a 0.05 step of m = 0.0625 of f = 0.009 of the animation - finer than anyone
  sees). Side effect, on purpose: `ExhaustedAttackSpeedPercent` 100 (m ≡ 1, attacks never slowed) keeps the animations at
  full speed too - "attacks never slow down" stays true to its word. `AthleticsRules.AttackSpeedFloorOf(percent)` is the one
  clamp (the decorator reads it without building the rules).
- **The range is 5-100 now** (was 0-100): the pure line at A = 0 reaches 0 at empty - a swing that never finishes. 5 matches
  `ExhaustedAttackSpeedPercent`'s floor (the old max(m, 0) never went below S ≥ 0.05). A config.json holding 0-4 is clamped
  with the usual `file problem` line. New label "Attack animation speed when empty (%)", new hint (the line, 92.5% halfway,
  the pause unchanged, 100 = full speed, attack speed 100 = never slower).
- Hot swap: unchanged and still live - `NoteRateSettings` marks every tired fighter (m < 1) dirty when the value changes, the
  decorator reads A live at each recompute (the smoke checks both, and the log line now names the line:
  `… get their attack animations x (0.50 + 0.50 f): full speed at the peak line, x0.75 halfway, x0.50 empty …`). Master switch:
  unchanged - `SpeedFactorsFor` returns "nothing" while ModEnabled / AthleticsEnabled is off, so the recompute the switch
  already asks puts every animation back to vanilla at once (the smoke's master-switch step still passes).

**The D / pause finding (the brief's point 3)**
- D is MEASURED from the real animation phases (`PhasesOnAction`): the wind-up = the ready's start to full wind-up (the
  `GetCurrentActionProgress(1)` poll), + the release's seconds, ranged + the reload's. All three are exactly what the decorator
  slows (SwingSpeedMultiplier, ThrustOrRangedReadySpeedMultiplier, ReloadSpeed). So a swing at ×0.85 is measured 1/0.85 =
  18% longer, and the pause D × (1/m − 1) would have grown by the same 18% - the brief's worry was real.
- Fix: D is built at FULL animation speed - each phase's played seconds × the animation multiplier it played at
  (`AtFullSpeed`; `TrackedAgent.PhaseAnimation` is taken when the phase opens, from the same m as `PhaseAsked`, so the wind-up
  and the release both use the pre-charge value - the recompute after the blow's charge lands a tick later, a ≤ 0.009 error
  on the release's tail). The pause is then exactly step 13's for the same attack; the slower swing adds only its own time
  to the cycle (at empty ~0.14 s on a 0.82 s melee attack). `CurrentWindUp` and `LastRest*` are in the same unit, so YOUR
  hold's estimate at the release's start (wind-up + last rest) × (1/m − 1) is consistent, and the Attack recovery bar (it
  reads your timer's pause) is unchanged. `AttackPlayed` / `EndedPlayed` / `PaceState.Played` carry the attack as played,
  for the logs and the stats only.
- **The stats** (`AttackRateStats`): the verdict's target was the fresh cycle ÷ m. The cycle now holds the slower attack, so
  the target adds `AnimationExtra` = the band's played attack (melee wind-up + swing; ranged + the reload) × (1 − the band's
  average animation multiplier) - "→ target 7.15 s (incl. +0.15 s of slower swing)". At 100 (or the peak) it is 0: the
  target is step 13's to the bit. The timer's floor ("D/m") = the attack AS PLAYED + its pause (the shortest cycle the spec
  allows now); the timer row prints "D avg 0.84 s at full animation speed - played 0.99 s, the slower swing" when the two
  differ (≥ 0.005 s), exactly as before otherwise. The first-timer lines (AI and yours) name the played attack the same way.

**Migration (config format 4)**
- `ConfigFile.FormatVersion` 4; `Migrate(read)` = `Migrate(read, p => p.Default)`. The OLD default is the one the file's own
  format knew: `MinMoveSpeedMultiplier` 0.7 → default only in a format-3 file (a format-2 file's default was 0.3 - its 0.3
  moves straight to 0.6 by step 14's rule, and a 0.7 there is his own); `AttackAnimationMinPercent` 100 → default in a format
  2-3 file (the key came with format 2). Logged `migrated config.json at …: MinMoveSpeedMultiplier: 0.7 → 0.6 (format 3 → 4,
  the old default; step 20b, …)` + `rewrote config.json as format 4 with the 2 migrated value(s)` (ConfigStore unchanged).
- **Why the seam** (`Migrate(read, defaultOf)`): the unit tests run on DESIGN's Default column, where MinMoveSpeedMultiplier IS
  0.7 and the animation 100 - "the default is back where it was: nothing to push" - and the schema's defaults cannot be swapped
  once built (`UseValuesForTests` throws after first use). The test hands in the shipped 0.6 / 85; a second assertion checks
  that on DESIGN's values the step-20b migration pushes nothing.
- **Anton's real config.json** (read only, 2026-09-28 09:46): format 3, `MinMoveSpeedMultiplier` **0.5**, `AttackAnimationMinPercent`
  **90** - his own playtest values, NOT the old defaults. The migration rightly leaves them (the brief's rule: a value he tuned
  stays), so **his next game will NOT run 0.6 / 85**: MCM → Defaults → "Revert all to defaults", or set the two by hand. And
  his 90 now means the line (×0.90 at empty, ×0.95 halfway), not step 13's max(m, 0.90) - gentler than the 85 he asked for.
  Said in PLAYTEST A1 and in the report to the manager. Not special-cased in code (a public migration must not move 0.5 / 90
  for everyone).

**Tests / smoke**: 401 → 405 (`AttackTimerTests`: the line 1 / 0.85 / 0.925 / 0.8875, 100 always 1, 0 = f, the range's 5 →
0.05, NaN / out of range, monotonic; f off the applied m round-trips the engine's curve, S 100 → full speed; D at full speed
keeps the pause (3.36 s). `AttackRateTests`: the slower swing in the target (on target, not "too slow"), the floor, the rows;
the settings sentence for 60 and 85. `ConfigFileTests`: format 3 → 4 both keys, tuned values kept each on its own, format 4
untouched, format 2's 0.7 his own / 0.3 → 0.6 / 100 → 85, DESIGN's values push nothing). Smoke 57 → 58 steps: the decorator
check now on the line (85 at empty 0.8925 swing and halfway 0.97125, 60 halfway 0.84 - the old rule gave 0.63 - a 0 held at 5
→ ×0.05, counters 6/8/1 and 5 AI recomputes); the hot-swap line; NEW `SlowerSwingKeepsThePauseInSeconds` - a wounded AI man
(f 0.4, ×0.91) plays his 0.4 / 0.5 s attack 1/0.91 longer and his timer is the full-speed 0.82 × (1/m − 1), the gap it
leaves the same, the first-timer line / timer row / band row name the played attack, back at 100 D = played. The older smoke
steps pin DESIGN's 100 / 0.7 (`AthleticsDefaults`), so they still test step 13's full-speed animations to the bit.

**UNVERIFIED — only the game can tell (PLAYTEST C1, C2, C6, L4, L5)**
1. The heavier swing is visible but not slow-mo → Anton's eye; the summary's `animations asked` x0.85 at empty and the band's
   swing `(x1.18)` of the peak's.
2. The pause in seconds did not change → the timer rows' `D avg` stays ~0.8 s melee (as in his 2026-09-28 logs) with `played …`
   beside it; `asked avg` = D avg × (1/m avg − 1).
3. The run at empty is 60% → `run speed check … empty (f 0) engine top x0.60 asked x0.60`.
4. The verdict does not turn "too slow" from the swing alone → the `incl. +… s of slower swing` in each tired band's target.

## Step 21 — battle pace: the shield wall swings less, archers shoot slower (DONE 2026-09-28)

Anton (2026-09-28, via the manager): "make the infantry more defensive, especially the guys with the shields, so that
maybe they swing 30% less (and make that adjustable)" and "to compensate make the archers a bit slower, maybe add a delay
in seconds that makes them fire about 30% slower overall". Goal: slower, more defensive battles (BATTLE_PACING's spirit - a
lever of his own). The manager's frame: through the EXISTING AI timer (guard up, by input), on top of tiredness
multiplicatively, AI only, a data check before fixing the formula. DESIGN §2 "Battle pace" is the spec; interpretation 23.

**The data check (Anton's real log, `trax_combat.log` of 2026-09-27 21:00 → 2026-09-28 10:57, read only)**
- Fresh AI melee (the `attack rate, melee, AI, peak (f 1)` rows; the input-technique battles, weighted by n):

  | battle | fresh cycle (n) | wind-up + swing = D | "pause" phase |
  |---|---|---|---|
  | 08:22 200v200 inf | 1.83 s (308) | 0.35 + 0.44 = 0.79 | 1.07 |
  | 08:30 200v200 | 1.78 (336) | 0.35 + 0.43 = 0.78 | 1.00 |
  | 08:43 200v200 (+ archers, crossbows) | 1.75 (200) | 0.30 + 0.44 = 0.74 | 0.97 |
  | 09:52 campaign field | 1.82 (89) | 0.32 + 0.39 = 0.71 | 1.17 |
  | 10:30 naval | 1.89 (41) | 0.30 + 0.44 = 0.74 | 1.18 |
  | 10:57 campaign field | 1.90 (28) | 0.48 + 0.57 = 1.05 | 0.90 |
  | **weighted** | **1.80 s** | **0.78 s** | → the AI's own gap G = 1.80 − 0.78 = **1.02 s** |

  (The NoAttack-era battles of 2026-09-27 read 1.61 / 1.86 / 1.70 - the same picture.) So the fresh cycle is 2.3 × D.
- **Does a pause hide in that idle time, or add to it?** The tired bands of 08:43 (by input) answer: f 0.5-1, timer asked
  avg 0.30 s → the next attack began 1.68 s after the attack's end = **1.38 s after the timer ended**, the band's cycle
  without a step back 2.04 s against the fresh 1.75 (+0.29 for a 0.30 s timer, 0.05 of it the slower swing); f below 0.5,
  asked 1.39 s → **1.51 s after the timer ended**. The AI's gap after a pause is its full ~1 s gap or more - a melee pause
  ADDS to the cycle, it does not hide (the raised guard: a man who wanted to attack and was made to block re-decides).
- **So the manager's first formula falls short**: m' = m × 0.7 on D alone gives a fresh man 0.78 × (1/0.7 − 1) = 0.33 s on
  a 1.80 s cycle = **16% fewer swings, not 30%**. Resized: the share stretches the whole EXPECTED cycle.
- Fresh AI ranged (`ranged, AI, peak`): 22:48 4.53 s (n 89 - Imperial Trained Archers, bows), 22:03 4.93 (117), 08:22 4.33
  (153), 08:30 4.49 (33), 08:43 4.98 (409 - Imperial Sergeant Crossbowmen + Palatine Guards), 09:52 4.98 (79 - Elite Hired
  Crossbow + Aserai archers), 10:30 5.52 (92 - Elite Hired Crossbow, naval), 10:57 6.22 (127 - with horse javelins); the
  training field's lone bowman 3.6 s. Weighted ≈ 5.0 s. **The log does not split bows from crossbows** (the ranged rows mix
  bows, crossbows and throws), so: bows ≈ 4.5 s (the bow-heavy battles 4.3-4.5 s), crossbows ≈ 6 s (their reload is ~1.5 s
  longer; the crossbow-heavy battles read 5.0-5.5 with bows mixed in). Ranged hiding: 08:43's fresh ranged pause phase was
  1.82 s and the gap after a tired pause 1.09-1.13 s (some hiding there); across battles the fresh ranged pause is 0.47-1.82
  (avg ~1.1 s) - about the gap after a pause, so an extra wait mostly adds.

**The formula (Core `BattlePaceMath.Plan`)**
- Foot melee, q = % / 100, T = step 13's tired pause D × (1/m − 1), G = `AiMeleeGapSeconds`:
  **pause = (T + q × (D + G)) / (1 − q)** → D + pause + G = (D + G + T) / (1 − q): the expected cycle ÷ (1 − q) at every
  tiredness - he swings q fewer than tiredness alone lets him (multiplicative, the brief's rule, on the right base). Fresh
  shield man (D 0.78, q 0.3): 0.3 × 1.78 / 0.7 = **0.76 s** (cycle 1.80 → 2.56, 30% fewer); fresh two-hander (q 0.15):
  0.15 × 1.78 / 0.85 = 0.31 s; a tired shield man at m 0.5: T 0.78 → (0.78 + 0.53) / 0.7 = 1.88 s (cycle 2.58 → 3.69).
- Archers: pause = T + the setting's seconds. Sized: extra ≈ cycle × (1/0.7 − 1) = 0.43 × cycle - bows 4.5 × 0.43 = 1.93 →
  **2.0 s** (4.5 / 6.5: 31% fewer); crossbows 6.0 × 0.43 = 2.57 → **2.5 s** (6.0 / 8.5: 29% fewer).
- Everything else: T alone (riders' melee, thrown weapons, slings); the player: never (his own timer, unchanged).

**Decisions (Claude's, 2026-09-28 - Anton can overturn any)**
1. **The class is read at each RELEASE from what he holds** (`IWeaponFacts` → `GameWeaponFacts`): a melee release → the
   off hand's current usage `IsShield`; a ranged release → the main hand's `WeaponClass` (Bow, Crossbow, else "other"); a
   throwing release → "other". `Agent.WieldedOffhandWeapon` / `WieldedWeapon` = the wielded slot index (a pointer the agent
   keeps - no engine call) + the managed equipment item: safe inside the OnMeleeHit callback where ObserveAction can run.
   On horseback = a rider whatever he holds ("infantry" = on foot). At the attack's END the class is checked against the
   kind that ended (`Consistent`; a mismatch falls back to the plain class of its kind - never a share it was not read for).
   A failing read logs one `[error] rate.class` and falls back the same way. Never for the player (no read at all).
2. **`AiMeleeGapSeconds` is a setting** (every number a parameter): the measured 1.02 s → 1.0, in the Battle pace group with
   a plain hint and a summary check beside it ("his own gap after a pause" vs the model's). It sizes both melee shares.
3. **Thrown weapons and slings get no share** (Anton said archers; javelin men throw a few and close in, their throws are
   limited by ammo; slings are rare). Their class is still measured (the "thrown and slings" line).
4. **Rides on the AI timer** (`AttackRatePaceHold` + the master switch + `AthleticsEnabled` - the poll that sees attacks runs
   only with Athletics on): off = no AI pause at all. Its technique (by input: guard up; NoAttack when switched), its lift
   paths (time, an attack slipping through, a game job, the player took him, left the field, mission end, the hideout fresh
   start), its survival through a step back - all unchanged and shared.
5. **Fresh men are held now**: `AskPause` holds a man at m 1 when his class has a share (the old "m ≥ 1 → not held" is now
   "m ≥ 1 and no share"). Paths that assumed no timer at the peak: the "AI timer" summary line (the peak band is listed once
   it has holds, and the line says fresh men wait too); the step back (still rolls only below the peak - untouched); the
   hideout fresh start (ends a share pause like any pause - the men start fresh and wait their share again at their next
   attack, as fresh shield men do); the recovery bar (the player's only - untouched); the timer rows (the share shown).
6. **Hot swap**: read at each attack's end - a change applies at every fighter's next attack; a running pause keeps its
   length; one `[rate] battle pace changed mid-mission …` line per change; the summary header flags a changed battle.
7. **The summary** - the model check is for foot melee only (the model needs G; the ranged gap varied 0.5-1.8 s across
   battles, too wide for one number): per class the attacks by N men, pauses by reason, the cycle and "a minute per man while
   fighting", the AI's own gap after a pause, and (foot melee) the model's cycle with the setting at 0 (D played + G + T) and
   with it (+ the pause) against the measured - at 0 the same line checks G itself. Archers: the extra's share of the cycle as
   an UPPER bound ("at most P% fewer - if none of it hid") - the honest in-battle number; the real check for every class is
   the same battle with the sliders at 0 and at their values (PLAYTEST D5 / D6).
8. **The class cycle** is filed under the class of the attack whose pause is inside it (the last ended AI attack of the same
   kind), a fighting rhythm only: the attack-rate cap at his m + the share inside it (so a big slider never pushes the
   shield wall's cycles out of the numbers). A pause asked for but not started (refused, superseded by his next attack)
   drops that cycle's model and counts as "no pause".
9. **The band verdicts** (attack rate by f) keep their meaning roughly: both the fresh reference and the tired bands carry the
   share multiplicatively, but shield and non-shield men mix inside "melee AI" - read the battle pace lines for step 21,
   the band lines for tiredness (said in PLAYTEST L6c).
10. **The MCM group "Battle pace (AI)"** sits after "Tired fighters step back" (group order 5; Refill → Advanced renumbered;
    Master switch first, Advanced last). The "swing less" sliders stop at 90 (100 would divide by 0). The AI timer's
    switch hint says the battle-pace waits ride on it. No config migration (new keys take their defaults).
11. **The seam for the smoke**: `WeaponFactsDefault.Current` - the game's read by default; the smoke swaps in its stand-in at
    its third step, before any logic is built (a native pointer read on its uninitialized fake agents would take the process
    down, uncatchable), and pins the new settings at 0 for the older steps (`BattlePaceOff` in `AthleticsDefaults`), so they
    still check steps 13-20 to the bit.

**Performance**
- Fresh men get pauses now, so nearly every foot soldier gets step 16's input component at his first attack (one small
  object per man, once - not per tick) and the engine's input callback is turned on / off per pause when we own it (two
  native flag calls per attack; with RTS Camera it is on for everyone and never touched). The idle callback is one bool.
- The AI timer's tick walks the held list: the smoke's cost step holds **1000 men at once** and times 500 ticks of the real
  `TickPace`: **~0.004-0.007 ms a tick, 0 bytes allocated** (AppDomain monitoring). No cap was needed; the existing start
  cap (100 a tick, `MaxHoldStartsPerTick`) stays. In game the summary's `Athletics tick cost` line is the check (step 16's
  playtest: 0.055 ms at 400 men).

**Built** - file map in CLAUDE.md "Layout"
- Core `BattlePace.cs` (AttackClass, RangedWeaponKind, PacePlan, BattlePaceRules, BattlePaceMath), `BattlePaceStats.cs` (per
  class + the 7 `[summary]` lines); `AttackRateStats` (the timer row's share, the peak band in the holds list, the AI timer
  line's wording); `SettingsSchema` (+5, the new group), `TraxSettings` (+5). defaults.json + refresh; DESIGN's table rows.
- Module `WeaponFacts.cs`, `AthleticsLogic.BattlePace.cs`; `AthleticsLogic.AttackRate.cs` (AiAttackEnded → NoteClassAttack +
  Plan → AskPause; the pause's parts in PaceState; the share in StartHold's stats, LogHold's text, RefuseHold /
  PaceAttackStarted → PaceNotStarted; NoteCycle / AttackBegan → the class cycle and gap; the settings lines; the summary);
  `AthleticsLogic.Engine.cs` (ReadAttackClass at each release); `TrackedAgent` / `PaceState` fields.
- Settings 78 → 83: `ShieldInfantrySwingsLessPercent` 30, `FootMeleeSwingsLessPercent` 15, `AiMeleeGapSeconds` 1.0,
  `ExtraPauseAfterBowShotSeconds` 2.0, `ExtraPauseAfterCrossbowShotSeconds` 2.5 (DESIGN's initial values = defaults.json).
- Tests 405 → 418 (`BattlePaceTests`: the class from what he holds, Consistent, a fresh shield man 0.771 s / 30% and the
  brief's D-only 16%, the percent fewer at every m for both melee classes, archers fresh and tired, riders and throws, 0 =
  off, ranges and NaN, the rules live from the settings, the settings sentence, the summary lines, at-0 and changed-flag).
- Smoke 58 → 61 steps (`Program.BattlePace.cs`): the stand-in weapon read (step 3), the battle pace step through the real
  logic (a fresh shield man 0.78 s by input with its first-of-class and first-timer lines, the class cycle 1.68 s and the
  model 1.82 / 2.60, a tired shield man (T + 0.546) / 0.7, a two-hander 0.321 s, a fresh rider none / a tired one T alone,
  a fresh bowman 2.0, a tired one T + 2.0, a horse archer 2.0, a crossbowman 2.5, a javelin none, the player never - no
  read, no count -, the slider to 0 and 50 live, the master switch lifting it, the summary lines), the cost step; the
  master-switch step also lifts a fresh shield man's share. deploy.ps1 green (build, AssemblyGuard, smoke, installed).

**UNVERIFIED — only the game can tell (PLAYTEST D5, D6, L6c; the line that settles each)**
1. The class read: a shield in the off hand reads as a shield, bows and crossbows by their class → one `[rate] battle pace -
   first <class> attack this mission: <name> …` line per class present, with the troops you expect (a legionary shield
   infantry, a voulgier other foot melee, a horseman a rider, an archer bowmen); no `[error] rate.class`.
2. **The shield wall swings ~30% less**: D5's A/B - `shield infantry … = X swings a minute per man` with the slider at 30 ÷ at
   0 ≈ 0.70; within the run `his own gap after a pause` near `AiMeleeGapSeconds` and `measured ÷ it` on target. If the gap
   after a pause reads well below 1.0 (the pause hid in his idle time), or the run at 0 reads `measured ÷ it` far from 100%
   (G is not 1.0 in that battle), retune `AiMeleeGapSeconds`.
3. **Archers ~30% fewer shots**: D6's A/B - `bowmen … = X shots a minute per man` at 2.0 ÷ at 0 ≈ 0.70, the same for
   crossbowmen; if the ratio is nearer 0.85 the extra partly hid - size the seconds up from the two X's.
4. Fresh held men keep their guard → L6b `GUARD: held by the timer` close to `everyone else`.
5. Cost → `Athletics tick cost` in a 400+ man battle near step 16's 0.055 ms; the input hook's `never called` ratio small.
6. Slower battles overall → D5's `after N s` (an 80v80 infantry fight ended in 4-5 min before step 16) and Anton's feel.
7. The crossbow is still in hand at its release (`WieldedWeapon` read at ReleaseRanged) → crossbowmen counted as crossbowmen,
   not "thrown and slings".

## Step 22 — defending costs the defender, the refill linear again, the run floor 0.3 (DONE 2026-09-29)

Anton (2026-09-29, via the manager, "to make battles slower"): "drop the faster athletics increase when empty - return it
to fully linear again all the way, 100% for 60 sec"; "make defending cost some athletics: defending with shield in the
right direction 1, with shield - wrong direction 5, without shield - 2 (params of course)"; "lower the floor max speed they
can run with when they get exhausted to 30% again".

**Verified in the v1.4.8 source (`..\reference\game-decompiled\TaleWorlds.MountAndBlade\`)**
- `AttackCollisionData` (public struct): `AttackBlockedWithShield`, `CorrectSideShieldBlock`, `IsAlternativeAttack`,
  `IsMissile`, `IsHorseCharge`, `CollidedWithShieldOnBack`, `CollisionResult` (`CombatCollisionResult`: None 0, StrikeAgent 1,
  HitWorld 2, **Blocked 3, Parried 4, ChamberBlocked 5**). `GetAttackCollisionDataForDebugPurpose(...)` is public static - the
  smoke builds real ones.
- `Mission.MeleeHitCallback`: `flag` = Parried || Blocked || ChamberBlocked; `isCanceled` (flag2) = … || (flag &&
  !AttackBlockedWithShield) - so a WEAPON block is "canceled" (no damage at all) and a SHIELD block is not (the shield takes
  damage through `GetAttackCollisionResults`); then `OnMeleeHit(attacker, victim, isCanceled, collisionData)` on every
  behaviour, ONCE per collision. `victim` = the agent whose guard stopped it = the DEFENDER. `CorrectSideShieldBlock` is read by
  the game itself (`MissionCombatMechanicsHelper`: `ShieldCorrectSideBlockDamageMultiplier` on the shield's damage) - the side
  is the game's own judgement. The shield on the back is its own flag (`CollidedWithShieldOnBack`).
- Missiles: `OnMissileHit` with `AttackBlockedWithShield` (and `MissileBlockedWithWeapon`) - a separate callback.

**Built** (file map in CLAUDE.md "Layout")
- `defaults.json`: `RegenRateNearFullPercent` 50 → **100**, `MinMoveSpeedMultiplier` 0.6 → **0.3**. Tunings: DESIGN's Default
  column keeps the INITIAL 50 / 0.7 (as step 20b did); the tests run on them. **100 is exactly linear** - step 14's flat branch
  (`FlatCurve`): r0 = 1/T, `RefillFrom` = x0 + r0 m t, `RefillSeconds` = Δx / (r0 m) - BlockTests checks it to 1e-12 and step by
  step through `Regen` (1/60 of the bar every second at a walk, full at 60 s).
- New settings (Athletics group, after `CostPerKickOrBash`, Float 0-20, 0 = free): `CostPerShieldBlock` 1,
  `CostPerWrongSideShieldBlock` 5, `CostPerWeaponParry` 2. 86 settings.
- Core `Block.cs`: `BlockKind`; `BlockMath.KindOf` - a missile, a kick / bash (IsAlternativeAttack), a horse charge or the
  shield on the back → None; the shield flag → right / wrong side by `CorrectSideShieldBlock`; else Blocked / Parried /
  ChamberBlocked → a weapon parry; else None. `BlockTracker` (a struct on `TrackedAgent.Block`). `AthleticsMath.BlockCostPoints`
  (the kind's setting × `CostMultiplier` - the blow's hero / leader multipliers), `ChargeBlock` (step 18's `ChargeKickOrBash`
  pattern: off / None / cost 0 = nothing; `Fighter.BlocksPaid`, never `Blows`; `Drain`).
- Module: `OnMeleeHit` got a LAST try block `BlockHitTaken` (after the attacker's part, so a release the poll had not seen yet
  is counted and his `ReleaseSerial` is current) → `BlockTaken(defender, kind, attacker index, attacker swing, now, rules,
  mounted)` (internal: the smoke drives it) → off: counted `BlocksWhileOff`; the tracker says the same blow: `BlocksSameBlow`;
  `ChargeBlock`; free: `BlocksFree`; else stats by kind and by who, the log, `AfterCharge` (the curves re-targeted, the peak
  line, exhaustion - exactly what a blow or a kick does after its points). NOTHING of the attack rate, the step back, the
  pause or the damage roll is touched. `OnMissileHit` counts missiles into a tracked fighter's shield (free).

**Decisions (Claude's - Anton can overturn any)**
1. **The regen delay**: a PAID block restarts it (`Drain` sets `LastBlowTime`), exactly as step 18 decided for kicks - a block
   is effort, and a man kept busy blocking should not refill (the point of "slower battles"). A block at cost 0 is nothing at
   all (no restart), as a kick at 0.
2. **Once per blocked blow**: `BlockTracker` keeps the defender's LAST blocked blow (the attacker's agent index, his swing =
   `ReleaseSerial`, the time). The same attacker within `SameBlowSeconds` 1.0 (plumbing, like step 18's SameActionSeconds)
   with the same swing - or no swing known (a couched lance: `IsDoingPassiveAttack`, or an untracked attacker → 0) - is the
   same blow; a new swing (a chained blow at once), another attacker, or 1 s later = a new one. Only the last blow is
   remembered: two attackers alternating collisions inside one swing each could charge one of them twice - rare (a blocked
   swing stops at the guard) and cheap (1-5 points); the summary's `the same blow seen again` shows the dedupe's work.
3. **Which blocks cost**: every tracked defender - you, AI heroes (their multipliers), common soldiers, riders (counted
   mounted). Free: missiles into a shield (Anton: "for now" - counted in the summary so a later price has its data), a blocked
   kick or bash (not blows since step 18), a blow the shield on the back stopped (no defence was made), a horse charge. A
   friendly blow that is blocked counts like any (rare).
4. **The attacker is unchanged**: his blocked swing costs him as before (released = charged; landed-only mode charges it at the
   hit - a shield / parry is a "hit" there, as it always was).
5. **The YOU line**: the first of EACH kind per battle is written in full (`[athletics] YOU: shield block (right side) at …
   cost 0.56 Athletics (1.00 x0.56 hero party leader) … - the first of this kind this battle: …`) - three lines at most; the
   rest go verbose with the AI's in bucket `athletics-block` (a big battle has hundreds of blocks - its own bucket, so blow
   lines cannot starve it).
6. **Migration (config format 5)**: the old default as THAT file's format knew it - `RegenRateNearFullPercent` 50 in format
   3-4 (the key came with format 3), `MinMoveSpeedMultiplier` 0.6 only in format 4 (a format-3 file's 0.7 already goes
   straight to today's default by step 20b's rule; a format 1-2 file's 0.3 IS today's default - nothing to push). **Anton's
   real config.json** (read only, 2026-09-29 06:40): format 4, `MinMoveSpeedMultiplier` 0.6, `RegenRateNearFullPercent` 50 -
   dry-run through the built Core DLL: exactly the two notes `RegenRateNearFullPercent: 50 → 100 (format 4 → 5, …)` and
   `MinMoveSpeedMultiplier: 0.6 → 0.3 (format 4 → 5, …)`, the three new keys missing (→ their defaults). His next game runs
   100 / 0.3 with the block costs on.

**Side effects to know**
- Shield walls drain now even when they barely swing - step 21 made them swing less and block more, so a pressed shield wall
  tires from blocking (1 per blow) and does not refill while it is hit. Step 23 (brace by orders) will lean on exactly that.
- A tired man held by the pause with his guard up (step 16's `AiHoldRaiseGuard`) pays for every block he makes: at empty he
  stays empty while he is pressed. That is the design ("a man under attack is not resting") - watch the summary's
  exhaustions and the `fighter-time by f` empty share against step 21's logs.
- With the run floor at 0.3 the run multiplier spans 0.7, so more 0.05 recompute steps per refill (~14 from empty to the peak
  line, as in steps 5c-13) - the per-tick budget (≤ 50) is unchanged.
- Performance: one struct compare + a few field reads per melee collision; no allocation (the verbose line is built only when
  `VerboseWants` says yes).

**Tests / smoke**: 418 → 427 (`BlockTests`: prices by kind × soldier / hero / leader for any pool, the settings' place and live
read + clamp, drain / BlocksPaid / the refill delay / exhaustion by blocks, free at 0 / off / None, the classifier, once per
blow, the refill at 100 exactly linear, the format-5 migration incl. tuned values kept and Anton's case, the settings
sentence); the pinned summary / settings strings and the format numbers of the older tests updated. Smoke 61 → 62 steps: NEW
`BlocksCostTheDefender` (Core's result codes = the game's enum; the classifier on REAL `AttackCollisionData` built by the
game's own factory - shield right / wrong, parry, chamber, landed, missile, bash; the real logic: 1 / 5 / 2 of 100, the same
swing again not charged, the delay restarted, no blow / pause / step-back roll, an AI hero 3.75, a leader's parry 1.13, you
0.56 with the YOU line once, ten wrong-side blocks empty a recruit and his speeds follow, live prices, 0 = free, Athletics /
the mod off = nothing); the master-switch step (a block while off costs nothing); the summary step (the blocks line; the
exhaustion counts +1 for that recruit). deploy.ps1: build, AssemblyGuard, smoke green, INSTALLED.

**UNVERIFIED — only the game can tell (PLAYTEST C1, C1d, L4)**
1. Blocks are seen in all three kinds → `[summary] Athletics blocks paid by the defender …: shield right side a, shield WRONG
   side b, weapon parries c` all > 0 after a melee; `WRONG side 0` in a big fight → `CorrectSideShieldBlock` is not what we
   think (tell Claude).
2. The player's lines → `[athletics] YOU: shield block (right side) … cost 0.56 …`, `… (WRONG side) … 2.81 …`, `weapon parry …
   1.13 …` - one of each kind per battle; the bar drops a little per block (Anton's eye).
3. The dedupe → `the same blow seen again` small next to the charged count (a large share → OnMeleeHit fires more often per
   blow than the research says - still charged once, fine).
4. The run at empty is 30% → `run speed check … empty (f 0) engine top x0.30 asked x0.30`.
5. The refill a straight line → `refill curve: flat (RegenRateNearFullPercent 100)`; `refill from empty to the peak line` avg
   ≥ 45.0 s, close to it when men walked.
6. The migration on Anton's file → `[config] migrated config.json at startup: RegenRateNearFullPercent: 50 → 100 …`,
   `… MinMoveSpeedMultiplier: 0.6 → 0.3 …`, `rewrote config.json as format 5 with the 2 migrated value(s)`.

## Step 23 — brace by orders: tired AI men stop swinging and defend (DONE 2026-09-29)

Anton (2026-09-29, via the manager): "soldiers get to 0% athletics but they still swing as much as they can - make soldiers
not swing but only defend when they have reached a certain floor, that depends on their current orders, until they have
replenished the floor +20%: standing, retreating or at halt 60% ... advancing 40%; charge 20% ... when defending they whip
out their shields if they have one". Mid-step addition (Anton): "+- 5% additive to those 20% he has to wait per soldier
(once rolled on a battle, adds some bravery-like randomness)" and a per-formation Bracing / Ready count for step 24.
DESIGN §2 "Brace by orders" is the spec; interpretation 25.

**Verified in the v1.4.8 source (`..\reference\game-decompiled\TaleWorlds.MountAndBlade\`)**
- `Formation.GetReadonlyMovementOrderReference()` → `ref readonly MovementOrder`; `MovementOrder.OrderEnum`
  (`MovementOrderEnum`: Invalid 0, AttackEntity 1, Charge 2, ChargeToTarget 3, Follow 4, FollowEntity 5, Move 7, Retreat 8,
  Stop 9, Advance 10, FallBack 11). Managed reads (`Agent.Formation` = a field). RBM reads it the same way.
- **The team AI advances on MOVE orders**: `BehaviorAdvance`, `BehaviorCautiousAdvance`, `BehaviorVanguard` set
  `MovementOrderMove(position)`; `BehaviorCharge` sets Charge / ChargeToTarget; `BehaviorTacticalCharge` alternates Charge
  and Move (reform, charge past); defend / hold-high-ground / skirmish behaviours use Move too; `BehaviorStop` Stop,
  `BehaviorRetreat` Retreat. So read literally every AI advance would sit on the hold floor (60). Decided: an
  AI-controlled formation (`Formation.IsAIControlled`) on Move reads its `FormationAI.ActiveBehavior` (a plain field
  getter): Advance / CautiousAdvance / Vanguard → "the AI's advance" (40), TacticalCharge → "the AI's tactical charge" (20);
  every other Move → hold. The player's own formations are not AI-controlled while he commands them - their orders are his.
- **Wielding**: `Agent.TryToWieldWeaponInSlot(EquipmentIndex, WeaponWieldActionType, bool isWieldedOnSpawn)` - native
  (`MBAPI.IMBAgent`); vanilla uses it for the victory cheer (`AgentVictoryLogic`, WithAnimation), scene animation points and
  after a game object. `WieldNextWeapon(HandIndex, …)` cycles (useless for "the shield"). `GetOffhandWieldedItemIndex` /
  `GetPrimaryWieldedItemIndex` read the agent's own memory (pointers); `Equipment[i]` is managed (`MissionWeapon.IsShield()`,
  `CurrentUsageItem.WeaponFlags` `NotUsableWithOneHand`, `IsRangedWeapon`, `Item.Weapons` = the usages).
- **The AI's own weapon choice is native** (no managed weapon-selection code in HumanAIComponent - its only wield hook is
  `OnAgentWieldedItemChange → DisablePickUpForAgentIfNeeded`). Whether the AI puts our shield away again can only be seen in
  game. Decided: best effort - checked every 1 s while bracing (`BraceMath.ShieldCheckSeconds`), at most 3 wield calls a
  brace (a one-hander, the shield, one re-try), and MEASURED: `the shield seen in his hand after we asked` vs `the AI put it
  away again while bracing` vs `never in his hand`.
- **`Agent.EnforceShieldUsage(UsageDirection)`** (native) holds the shield up (vanilla's shield wall: front rank DefendDown) -
  NOT used: `Agent.ApplyFormationValuesPostUpdate` calls `UpdateFormationOrders()` for every AI agent on each formation
  update, which rewrites it from the arrangement (`ArrangementOrder.GetShieldDirectionOfUnit` - None outside shield wall /
  circle / square); RBM keeps its own only by a Harmony prefix on `UpdateFormationOrders`. We have no Harmony, so the shield
  is kept up through the input instead: `BraceRaiseShield` - `AiInputMath.RaiseShield` ORs `DefendDown` into a frame with no
  attack and no guard of his own (his own block directions stay his).

**The build (Claude's calls - Anton can overturn any)**
1. **The bar** = `Fighter.Fraction` (points ÷ the FULL pool) - what the fill shows, not f.
2. **The band** (`BraceMath.Band`): floor F by order, his margin M = `BraceRecoverPercent` + his offset (≥ 1 point), the top T
   his wounds allow: target = min(F + M, T), floor = min(F, max(0, T − M)). Enter at or below the floor while below the
   target; leave at or above the target. Under a wound cap the band keeps its width and slides down (a man capped at 50 under
   hold braces 30 → 50) - never stuck, never bracing full to his cap. Ends: the group changed since the start → "the order
   changed"; else capped → "his wound cap"; else "refilled".
3. **The spread**: each AI man rolls a unit u ∈ [−1, 1) ONCE at his first look (`BraceDice`, ThreadSafeRandom in game, seeded
   in the smoke) and keeps it; offset = u × `BraceRecoverSpreadPercent` read LIVE (a slider change rescales everyone at once,
   each keeping his place - simpler and truer to "once rolled" than re-rolling). Default 5.
4. **The look**: `TickBrace` every tick - switched off → every brace lifted at once (and a line); else a slice of the dense
   list so each man is looked at about every 0.25 s (`PollSeconds`, plumbing; a call after a longer gap looks at everyone).
   Per look: IsActive, the player / not AI-controlled check, the order, the band - no allocation (the BraceState is made once
   per man). Measured: 1000 men, 400 bracing, **0.003 ms a tick, 0 bytes**.
5. **The hold**: a fourth wish on step 16's `AiInputState` (`BraceHoldAttacks`) - `Active` is ANY wish, so the AI timer's end,
   the backpedal's end and the brace's end never lift each other, and `UnhookIfIdle` keeps the callback on while any runs.
   The same `AiInputMath.HoldAttacks` (attack bits out, a guard when he wants to attack, a ready cancelled). The brace is
   input-only (no NoAttack variant - `AttackRatePaceByInput` does not switch it).
6. **Ranged goes on**: the look reads his hands (`BraceRanged` = a ranged weapon in the main hand) and the frame lets
   through a ranged action under way (ReadyRanged, ReleaseRanged / Throwing, Reload); a bracing man's melee release is
   counted (should be ~0), a ranged one counted apart (allowed).
7. **Who**: AI men on foot and mounted, AI heroes; never the player (`IsPlayer`), and a man the player takes over (not
   AI-controlled) ends his brace ("the player took him"). The step back (it rolls at a swing's end - bracing men do not
   swing) and battle pace are untouched. The hideout fresh start ends a brace. Leaving the field ends it without an engine call.
8. **The shield**: at the start (`BraceWieldShield`): in hand → nothing; a ranged weapon in hand → left alone; a weapon that
   needs both hands → a one-handed weapon first (an item with a one-handed melee usage), the shield at the next check; no
   one-hander → left alone; else the shield. At the end nothing is forced back (the brief: let the AI choose).
9. **Formation read API**: `FormationAthleticsStats.Bracing / Ready / Total` from the refresh pass (step 24 reads it).
10. **Logs**: `[brace]` mission start (the settings sentence), a settings change (not on a master-switch toggle - SameAs
    compares BraceEnabled), a switch-off line, the first brace and its end in full, verbose per brace (bucket `brace`); the
    7 `[summary] brace` lines (who, by order, time share, lengths + still bracing at the end - the "turtle" check, ends by
    reason, margins and length by margin, the shield, the GUARD while bracing + the hook's frames + attacks while bracing +
    braces the engine never called us during).

**Built** - file map in CLAUDE.md "Layout". Core `Brace.cs`, `BraceStats.cs`; `AiInput.cs` (RaiseShield); `SpreadStats.cs`
(Bracing / Ready / Total). Settings 86 → 94 in a new MCM group "Brace by orders (AI)" after Battle pace (Refill → Advanced
renumbered 7-11): `BraceEnabled` true, `BraceFloorChargePercent` 20, `BraceFloorAdvancePercent` 40, `BraceFloorHoldPercent` 60,
`BraceRecoverPercent` 20, `BraceRecoverSpreadPercent` 5 (0-50), `BraceWieldShield` true, `BraceRaiseShield` true. No config
migration (new keys take their defaults). Module `BraceBody.cs`, `AthleticsLogic.Brace.cs`; `AiInputHook.cs` (the brace wish,
SetBrace, the frame counts); the tick, settings change, Forget, ObserveAction, OnMeleeHit, the summary, the hideout fresh start,
the formation refresh wired. Tests 427 → 442 (`BraceTests`). Smoke 62 → 65 steps (`Program.Brace.cs`: the stand-in body at
step 4, the real logic end to end, the cost; the master-switch step lifts braces). deploy.ps1 green, INSTALLED.

**UNVERIFIED — only the game can tell (PLAYTEST D7, L6d)**
1. The engine honours the brace frame → `melee attacks that started while bracing anyway` ~0, `braces the engine never called
   our input hook during 0`.
2. The order read matches what the player gave and what the enemy's AI does → `brace: … by order: …` (`stop / hold` in a hold,
   `charge` after F1 → Charge, `the AI's advance` for the enemy's approach) and `the order changed` > 0 after a charge.
3. **Lines do not turtle forever** → `brace lengths` mostly under 15 s, few `still bracing when the battle ended`; blocks cost
   Athletics (step 22) and restart the refill delay, so a man pressed hard may not refill at all - if the lengths are long,
   the fix is a cheaper `CostPerShieldBlock`, lower floors or a smaller margin (all sliders).
4. **The shield comes out and stays** → `the shield seen in his hand after we asked` vs `the AI put it away again while
   bracing`; if the AI always puts it away, the next try is re-asserting every check (raise `MaxWieldCalls`) - tell Claude.
5. The raised shield helps → `brace GUARD` blocked % ≥ the AI holds GUARD line's `everyone else`; if bracing men are slowed
   too much walking with the shield up (they lag their formation), switch `BraceRaiseShield` off and compare.
6. Two-handers switch to a one-hander and shield (`a one-hander first` > 0 and later `taken out`), and their AI picks the
   two-hander again after the brace (Anton's eye).
7. Cost → `Athletics tick cost` in a 400+ man battle close to step 22's.

## Step 24 — the ready count per squad on the ALT labels and the orders strip (DONE 2026-09-29)

Anton (2026-09-29, via the manager): "can you give me some indication above the squad of ready men, that are not resting
in their current order status, somewhere near the athletics info on the ALT and on the formations view". DESIGN §3 (the
strip's and the markers' "as built" + interpretation 26).

**The data** - step 23's read API, nothing new in the logic: `FormationAthleticsStats.Ready` (= Count − Bracing) of `Total`
(= Count), from the formation refresh pass every `FormationStatsRefreshSeconds`, every team. The player is left out of his
own formation there (step 9), so the count agrees with the strip's other numbers and with the card's own count; under an
ALT marker of HIS formation it may read one fewer than vanilla's count above it (vanilla counts him) - stated in DESIGN.

**Claude's calls (Anton can overturn any)**
1. **Words**: `ready 34/50`. "34/50" alone would not say what it counts; a longer word would not fit. Same text both places.
2. **Colour** (`ReadyMath.Band`, "at or below", the most alarming wins): plain = the text brushes' own `#E8E8E8FF` (both
   `AgentHUD.Interaction.Text` and `NameMarker.Distance.Text` in the game's `Native\GUI\Brushes\Mission.xml`), so the line
   looks like its neighbours until it warns; yellow at or below `ReadyYellowBelowPercent` 75 (a quarter bracing), red at or
   below `ReadyRedBelowPercent` 50 (half the line bracing) - the bars' own yellow / red. Two thresholds, not the bar's three:
   enough for "fine / watch it / trouble".
3. **Hidden while bracing cannot happen** (`ReadyRules.Hidden`: ModEnabled first, AthleticsEnabled, BraceEnabled, then
   `ShowReadyCount`) - picked over "all ready" (less noise). An empty formation: no line (its cell / label is hidden anyway).
4. **One switch for both places** (`ShowReadyCount`, in the "Orders menu strip" group beside `ShowFormationHealth`, which
   also serves both); the thresholds beside it.
5. **The strip**: a third row UNDER the bar, left, at `OrderStripReadyOffset` 24 (Advanced). Why not beside the numbers: a
   131 px card leaves 35 px each side of vanilla's 60 px order-icon pair, and "72% ± 8" already takes ~34. The cell becomes
   38.6 px deep from +1 = it ends at card bottom + 39.6, inside the 40 px gap between two stacked cards. The cost: at
   1920 × 1080 a column's bottom card has 24 px to the screen's edge, so its cell is LIFTED 16 px (the existing lift - its
   numbers then sit on the card's lowest 16 px, beside the icons). `StripLayout` counts the row only while it is drawn
   (`ReadyRow` = `ReadyRules.Shown`), so with bracing off the cell is exactly step 9's 23 px and nothing is lifted. The
   layout follows a settings change at once (`OnLayerFrame` re-applies it on the settings version), the values at the next
   refresh. The panel rows carry it after the health (the panel is 300 px wide - it fits).
6. **The ALT labels**: a third line under the bar, centred, `MarginTop 1` like the bar (a prefab constant like the bar's).
   The label's box grows by one line (CoverChildren) - no layout setting needed. Every side's labels (the enemy braces too).
7. **Refresh**: the views' existing pushes - the strip at `HudRefreshSeconds` when the stats version moved (a settings change
   forces one), the ALT labels when the stats or the settings version moved (per label, int compares per frame). Texts rebuilt
   only when a number changes (`SetReady`), colours are cached `Color`s (`OrderStripCellVM.ReadyColors`, shared). No
   per-frame allocation added.
8. **Logs**: the values lines (`… HP 81% ready 34/40 (40 men, …)` + the tail `…, ready count on: yellow at or below 75%, red at
   or below 50% of the men ready` / `ready count hidden (BraceEnabled off - nobody braces)`), the strip's first placement names
   the row (`ready count at +24 … 39 px deep`); one new [summary] line per view: `hud: orders strip - ready count (men not
   bracing, step 24): shown N x (plain / yellow / red), the fewest ready 12/50 (24%, 1 Infantry); hidden N x (reasons)`.

**Built** - Core `ReadyCount.cs` (ReadyBand, ReadyHidden, ReadyRules, ReadyMath, ReadyCountStats), `OrderStrip.cs`
(StripLayout ready row, DescribeValues + ready), `AltMarkers.cs` (DescribeValues + ready); settings 94 → 98
(`ShowReadyCount` true, `ReadyYellowBelowPercent` 75, `ReadyRedBelowPercent` 50 in "Orders menu strip"; Advanced
`OrderStripReadyOffset` 24 - range −40..80); no config migration (new keys take their defaults). Module: the two VMs
(ReadyText / ReadyColor / ReadyShown, the strip's ReadyMarginTop), the two views (push, stats, summary), both prefabs (a
TextWidget with `Brush.FontColor` bound - the same binding the ALT Athletics number uses). Tests 442 → 460
(`ReadyCountTests`). Smoke 65 → 67 steps (`Program.Ready.cs`; the older strip / ALT steps pin `ShowReadyCount` off so their
exact log lines stay step 9 / 20's). Gotcha met: a `static readonly Color` on the smoke's `Program` loads TaleWorlds.Library in
its type initializer, BEFORE Main hooks the resolver - "Could not load file or assembly TaleWorlds.Library"; keep game-typed
statics in a nested class (as `AltColors` did).

**UNVERIFIED — only the game can tell (PLAYTEST D7 "The ready count", E4, L7)**
1. The line draws where planned: under the strip's bar without touching the next card (eye; the first placement line's
   `ready count at +24 … 39 px deep`), and the bottom card's lifted cell reads fine at Anton's resolution - else
   `OrderStripReadyOffset` / `OrderStripTextSize` live, or tell Claude to move it.
2. The count moves with the braces in a hold-then-charge battle - falls and turns yellow / red while holding, jumps back at
   the charge → the two `[summary] hud: … ready count` lines (yellow and red > 0, the fewest ready plausible) and the eye.
3. The colours read on the battlefield (the plain one is the brushes' own; yellow / red the bars').

## Step 25 — overall damage reduction (DONE 2026-10-10)

Anton (2026-10-10): "add 25% overall damage reduction on all sources (configurable), so if I do 100 dmg with a swing
on average -> 75 dmg. This will further slow down the combat."

**Built**: setting `DamageReductionPercent` (int, 25, range 0-90, group "Damage" - renamed from "Damage randomness";
MCM groups are by index, so nothing saved moves), Core `DamageReduction.cs` (ReductionSkipReason, ReductionOutcome,
Decide / Apply / Reduce / Clamp / Multiplier / Example), `DamageRules.ReductionPercent` (read in the same
`DamageRules.From` as the roll - one settings read per hit), `DamageStats` reduction counters + one [summary] line;
module `DamageRandomizer.Reduce` called on BOTH paths of `Apply` (rolled → on `roll.After`; skipped → on the game's
value), the first reduced hit of a mission always logged, ", reduced → N" on the verbose roll / skip lines, the
mission-start line extended AFTER "- read live on every hit" (every older substring check still matches).

**The order per hit**: BaseModel.ApplyGeneralDamageModifiers (armor etc.) → the roll → the reduction → the game's
`MathF.Round`. Same rounding rule as the roll: a value that SHOWS as 0 (≤ 0.5, banker's) is returned untouched; a
positive one is `max(1, x · (1 − r/100))`. Clamped to 0..90 twice (the schema's range, `DamageReduction.Clamp`).

**Which hits - decided per roll skip reason** (DESIGN interpretation 27):
- objects (`!IsColliderAgent`: gates, rams, siege engines, ships) - NOT reduced: a men's-fight setting must not
  lengthen sieges or naval hull damage.
- no victim - NOT (defensive, not expected).
- fall - NOT: not a blow; a jump from a wall should not grow safer.
- a 0 hit stays 0 (a blocked blow's vanilla 0 is never touched; CalculateDamage only runs for damage > 0 anyway).
- `DamageRandomEnabled` off / spread 0 / the melee, ranged, mount and shield toggles - STILL reduced: the roll is
  "random", the reduction "weaker"; one off must not switch the other off.
- shield blocks - reduced (with or without `DamageRandomOnShields`): "all sources", and a shield lasting a third
  longer is the same "slower". No toggle - one setting to add if Anton wants shields at the game's pace.
- kicks / bashes, horse charges, missiles, missile area damage, friendly fire, tournaments - all reduced (they all
  come through `ApplyGeneralDamageModifiers` as agent hits - RESEARCH §A; step 4's verbose lines name them).
- master switch off - NOT reduced (pure vanilla), counted as `mod OFF` LAST (after the structural rules and the
  setting at 0) like the roll's ModOff, so an OFF battle counts the hits an ON battle would reduce.

**Not touched**: stagger / knockdown / dismount still follow the final damage (game rules, DESIGN §1) - so they come a
bit less often; stun periods, shrug-off thresholds and `CalculateShieldDamage` / sail / hull fire are not ours.

**Config file**: no migration - a key absent from config.json takes its default ("missing → default", the file is
rewritten with it; ConfigFile.FormatVersion stays 5). Anton's own config.json gets 25 at its first read.

**Logs**: `[damage] mission start: … - read live on every hit; overall damage reduction (DamageReductionPercent) 25% after
the roll, rolled or not (an average 100 hit lands 75; falls and objects never)`; `[damage] first reduced hit this mission -
<hit>: the game's 18 → after the roll 23 → reduced 25% (DamageReductionPercent) → lands 17`; `[summary] damage reduction
(DamageReductionPercent 25%, after the roll): N hits (melee …, ranged …, mounts …, shields …); damage the game's A → after
the roll B → landed C (-25.0% from the roll's, …% from the game's), avg … per hit (the game's …); not reduced: …`. The roll
lines above it are unchanged (the roll alone, before the reduction).

**Tests**: DamageReductionTests (+22 → 482). **Smoke**: `DamageReductionThroughDecorator` (68 steps); the older damage
steps pin `DamageReductionPercent` 0 in `DamageDefaults()`; the master-switch step sets 25 and checks 51 hits counted
`mod OFF`, none reduced.

**Gotcha (this machine, 2026-10-10)**: the D: drive (Directory.Build.props' game path) was not mounted; the build reads
`Directory.Build.props.user` (C:\Program Files (x86)\Steam\…), and `tools\deploy.ps1` has its own D: defaults - run it
with `-GameFolder` / `-McmBinFolder` pointed at the C: install. The smoke passed; the install copy hit a DLL lock (the
game was running) - deploy again once it is closed.

**UNVERIFIED - only the game can tell (PLAYTEST C8 / L3)**
1. The "Delivered N damage" numbers read about a quarter lower and men take visibly more blows - and it FEELS slower
   without being a slog (is 25 right?).
2. Shields lasting longer feels right (else: a shield toggle).

## Step 26 — launch prep: thumbnail, gallery, description, README (DONE 2026-10-10)

Anton (2026-10-10): "the mod I start to enjoy very much. Start prepping it for launch - it makes the combat slower,
makes tactics more important, makes heroes a bit stronger and more of key figures." Model: `..\better-skirmisher-separation`
(its README, STEAM-DESCRIPTION, make_thumbnail / make_workshop_update, screenshots/ git-ignored). Contrast named: RBM, politely.

**Screenshots.** Steam keeps F12 shots in `C:\Program Files (x86)\Steam\userdata\258577504\760\remote\261550\screenshots`
(3440x1440). The 2026-10-10 batch: 20:52:26-20:54:05 = five battle shots (ours, ALT labels + strip + bars on screen);
20:58:32-20:59:12 = eight shots of another mod's "Party Steward" town screen - NOT ours, left out. Copied to
`/screenshots/originals/` (git-ignored like the sibling; the .gitignore line is `/screenshots/` - an unanchored
`screenshots/` also hid `docs/screenshots`).

**Thumbnail** (`tools/make_thumbnail.py` → `tools/preview_thumbnail.jpg`): the sibling's gold frame / Palatino look over a
real battle (205310: shield wall + archers), a magnified hold-ALT close-up (205359: the enemy's 16% ± 12 "ready 1/9" in red
beside your 79% ± 25 "ready 37/53") under a "Hold ALT · who is tired?" tab, your Attack recovery + Athletics bars (205226),
then "Trax / Combat Enhancements / Slower, tactical battles. Heroes matter." Iterated by eye 7 times: crop wide enough to
read as a battle; the order-menu column (x < ~480) cropped off; far formations' labels above the troops painted out
(ERASE: clone the same rows 240 px right); the inset's troop counts "9" / "53" painted out (the tab sat on them); the tab
OUTSIDE the inset (inside it covered either the markers or "ready 1/9"). Checked at 256 px. JPEG, not PNG: a photo PNG
is ~1.5 MB, over Steam's 1 MB; the old PNG (no game art) is deleted and WorkshopCreate's <Image> points at the .jpg.

**Gallery** (`tools/make_gallery.py` → `docs/screenshots/01..05`, 2560 px, ~600-700 KB): ALT, shield wall, orders strip,
your bars, a mounted hero. The uploader cannot add gallery images (the sibling's finding) - by hand on the item page.

**Description** (6009 bytes): pitch (3 lines) + "Not an overhaul" paragraph, then What changes / Tired men act like it /
Heroes matter / See it / Make it yours / Compatibility / FAQ / Removing it / First release / credits (the sibling's
"made by two of us" + "read my top pinned" lines). Every number checked against defaults.json (10 / 3 / 1-5 costs, 0.75 x
0.75, 20% attack rate = 1 in 5, run 0.3, animation 0.85, 25% reduction, 30 / 15 % swings, brace 60 / 40 / 20) and the MCM
labels quoted against SettingsSchema ("Damage reduction", "Shield infantry swing less", "Brace floor").

**Found and fixed**: WorkshopCreate/Update.xml pointed at `C:\Users\Trax\Documents\BannerlordMods\...` - no such folder on
this PC; now `C:\Users\Asus ROG\Documents\BannerlordMods\trax_combat_enhancements` and the C: Steam game (the uploader exe is there).
Gotcha for next time: Python heredocs with Windows paths ate `\b` (backspace) and `\261` (octal ±) - check edited files for
control characters, or use the Edit tool.

**Package dry run** (C: game: `-GameFolder` / `-McmBinFolder`, the defaults are on the unmounted D:): build, unit tests,
AssemblyGuard, offline smoke all passed; v0.1.0, 9 files, 833,104 bytes; zip 328,497 bytes (dist/ git-ignored). Version NOT
stamped - v0.1.0 vs v1.0.0 is Anton's call.

**For Anton before upload**: look at the thumbnail and gallery; pick the first version number; the Workshop tags stay
Utility / UI / Native / Singleplayer / v1.4.8 (add a "Battle" or "Gameplay" tag on the page if Steam's list has one);
upload = his yes only.

## Step 27 — release v1.0.0 (DONE 2026-10-10)

Anton's yes in chat: "v1.0.0, go ahead and upload it, git and steam live".

- **Workshop item 3817259431** — https://steamcommunity.com/sharedfiles/filedetails/?id=3817259431. Created by
  `WorkshopCreate.xml` (Private, short pitch, tags, preview), then ONE update from `make_workshop_update.py --notes
  "v1.0.0 - first release." --visibility Public` (the full STEAM-DESCRIPTION.bbcode + Public). Never run the create file again.
- **Verified** without a browser: `POST https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/`
  with `itemcount=1&publishedfileids[0]=3817259431` (no key needed) → result 1, visibility 0 (= Public; 1 friends, 2 private),
  file_size 833067, the description in full. Handy check after every later update.
- **Version / commits**: v1.0.0 stamped in 4cd40cd (pushed before packaging; the DLL is built from it) - tag `v1.0.0` points
  there. GitHub release v1.0.0 carries `TraxCombatEnhancements_v1.0.0.zip` (328,486 bytes).
- **Uploader quirks seen** (all as documented): the crash at the end on `Console.ReadKey` (non-interactive) after
  "Uploading done!" - harmless, exit code meaningless; PowerShell 5.1 wraps its first stderr line ("Setting breakpad minidump
  AppID") as a NativeCommandError - also harmless. The update with no `<Image>` only shows the CommittingChanges status
  (no content changed since the create - Steam skips the identical upload). Droppings `steam_appid.txt` /
  `steam_workshop_uploader.txt` at the repo root, git-ignored.
- **Left to Anton** on the item page (Owner Controls → Add/edit images & videos), in order: `docs\screenshots\`
  01-hold-alt-who-is-tired.jpg, 02-shield-wall-and-archers.jpg, 03-orders-menu-strip.jpg, 04-your-bars.jpg,
  05-hero-in-the-field.jpg. Optionally subscribe and check the log says `module: TraxCombatEnhancements (the release)`.

## Step 28 — duel deadlock fix, part 1: the no-formation floor and each man's own floor (DONE 2026-10-12)

Anton (2026-10-12, a tournament log): both 1v1 fighters got exhausted and the AI blocked ~95% of his swings. The log:
tournament / arena fighters have NO formation (`Agent.Formation == null` → `BraceOrderKind.NoFormation`), and step 23 put
"no formation" in the hold group - floor 60%: 11 of 16 AI braced, 62% of their field time, the longest brace 163 s. The
manager's brief: no formation its own floor (20), and every AI man's FLOOR his own roll (±10), apart from the ±5 margin roll.

**The build (Claude's calls - Anton can overturn any)**
1. **A fourth floor group** `BraceOrder.NoFormation` (Core `Brace.cs`): `BraceMath.Group(NoFormation)` → it, `FloorPercent`
   → `BraceFloorNoFormationPercent` (20, 0-100). The hold group keeps stop / move / retreat / fall back / follow / no order
   (NoOrder = a formation with an Invalid order - kept on hold: a real formation, just unread). Names: "no formation" in the
   first-brace line ("the no formation floor 20%"), the summary's `by floor: … no formation N`, the settings sentence
   ("no formation (tournaments, arenas, duels) 20% → 40% (BraceFloorNoFormationPercent)"; the hold clause says "no order"
   now, not "anything else"). Who has no formation in the game: tournament / arena fighters, a hideout duel's two, any agent
   the mission spawned outside a formation (e.g. some scripted fights) - all the same floor.
2. **His own floor**: a SECOND unit roll `BraceState.FloorUnit` ∈ [−1, 1) at his first look, ONCE per man and battle, kept;
   `BraceMath.FloorOffset = unit × BraceFloorSpreadPercent / 100` read LIVE (the slider rescales everyone at once, each
   keeping his place - the margin's pattern); `BraceMath.Floor = clamp01(order floor + offset)`; `Band(r, order, top, unit,
   floorUnit)` uses it as F - so the wound-cap rule, the hysteresis (target = F + his margin ≥ F + 1 point) and the "never
   stuck" slide are unchanged. `SetFloor` in the band is now HIS floor (the log shows the order's floor beside it).
   Default 10 (range 0-30, the brief's "e.g."). Everywhere: every order, no formation too.
3. **Independent dice**: the floor roll comes from its own source, `AthleticsLogic.BraceFloorDice` (ThreadSafeRandom in
   game - the same thread-static Random, so the two draws are simply two draws), seeded APART in the smoke - so step 23's
   seeded margin sequence is unchanged (the smoke checks the first man's margin = the first draw of SeededRandom(23)).
4. **Clamping**: a floor under 0 → 0 (he braces only when empty, target = his margin); over 100 → 100, and then (step 23's
   rule for any band that would reach past the top) the band keeps its width M under the top: floor = min(F, 1 − M) - a
   floor-95 man with +10 braces at 80 and stops at full, never "braces at full".
5. **Stats / logs**: `BraceStats.AddMan(unit, floorUnit)`, `AddEnd(…, floorUnit)`; the summary is 8 lines now - after
   `brace margins` a `brace floors (BraceFloorSpreadPercent 10, step 28): N men rolled, their floor offsets now min / avg /
   max points on the order's floor; braces by his own floor: low (−, fights on longer) …, middle …, high (+, braces sooner)
   …` (thirds of the unit, like the margin bands). The first-brace line: `order no formation (the no formation floor 20%;
   his own floor 23.4% = + 3.4 points by his own roll, BraceFloorSpreadPercent 10), … until 44.9% = his floor 23.4% + his
   margin …`. The settings sentence names the floor roll only when it is above 0.
6. **No config migration**: a config.json without the two keys reads them as their defaults (`ConfigFile.Apply`: missing →
   default) and `ConfigStore` writes them in at that read ("added 2 missing setting(s) to config.json with their defaults")
   - FormatVersion stays 5. Settings 99 → 101.

**Tests / smoke**: Core 482 → 487 (`BraceTests`: the no-formation floor 20 → 40 and the old 60 gone, its own live slider,
the hold floor untouched; the floor roll ±10 on hold / no formation / charge, independent of the margin (floor and target
from each), live rescale 0 / 10 / 30, NaN / out-of-range units, clamped at 0 and 100 with the hysteresis over 2000 random
rolls, the wound cap sliding his own band (70 → 90 capped at 50 = 30 → 50); the summary's new line and the `no formation`
count). Smoke 68 → 69 steps: `BraceNoFormationAndFloorRoll` (the real logic: a no-formation man fights past 60 and braces
at the first blow under 20 + his roll, stops at his floor + 20, his roll kept, the margin's sequence unchanged, both
sliders live, a holding man on 60 ± his roll, the lines); step 23's checks pin `BraceFloorSpreadPercent` 0
(`BraceDefaults`), the 1000-man cost check runs with it at 10 (0.003 ms a tick, 0 bytes).

**UNVERIFIED — only the game can tell (PLAYTEST D8)**
1. Tournament fighters really read as no formation → the summary's `by floor: … no formation N` > 0 and `hold and the
   rest` 0 in a tournament. If a tournament puts its fighters in a formation after all, the next fix is to read the mission
   (tournament / arena behaviour by name, like the step back's check) - tell Claude.
2. The 1v1 becomes a fight → `time bracing` share far below 62%, the longest brace far below 163 s. Steps 29 (only
   wrong-side blocks restart the refill) and 30 (tired AI blocks in a random direction) are the rest of the deadlock fix.
3. Field battles: a holding line braces man by man between ~50 and ~70 → `brace floors` offsets spread ±10, braces in all
   three floor bands.

## Step 29 — duel deadlock fix, part 2: only a wrong-side shield block stops the refill (DONE 2026-10-12)

Anton (2026-10-12, the same tournament 1v1 as step 28): both fighters reached empty and STAYED there. The refill starts
`RegenDelayBlowTimes × BlowTimeSeconds` (3 s) after a fighter's last blow - and since step 22 every PAID block was a "blow"
for that clock (`Drain` set `LastBlowTime = now`), so a man defending against a swing every ~1.5 s never refilled. Anton's
rule: a SKILFUL defender should recover while staying on the defence. So only a WRONG-side shield block restarts the delay.

**How the delay is represented (checked before building)**: one clock, `Fighter.LastBlowTime`; `Regen` refills only the
part of each step after `LastBlowTime + RegenDelaySeconds`. `Drain` (the shared body of every charge) also set
`Regenerating = false`, ending the current refill RUN (only the episode bookkeeping: start fill, seconds, walk share -
the "refill from empty to the peak line" summary line). Nothing else gates the refill: `RegenPass` skips a man only at his
top (a block drops him below it, so he is looked at again), the effort is sampled from his speed every step, and the
HUD's "refilling" reason (`BelowFull`) reads the fill. So a non-restarting block must leave exactly two fields alone.

**The build (Claude's calls - Anton can overturn any)**
1. **One switch** `OnlyWrongSideBlocksStopRefill` (bool, DESIGN initial true, defaults.json true; Athletics group right
   after `CostPerWeaponParry`; live - read in `AthleticsRules.From` at each block). Off = step 22 to the bit. Settings
   101 → 102. No config format bump: a missing key reads as its default and `ConfigStore` writes it in.
2. **The rule in one place**: `AthleticsMath.BlockRestartsRefill(r, kind)` = kind ≠ None && (switch off || kind ==
   ShieldWrongSide). `ChargeBlock` passes it to `Drain(…, restartDelay)`: false = only the points move - `LastBlowTime`
   and `Regenerating` untouched, so a running refill run simply continues (the bar dips by the cost and climbs on, the
   episode keeps its start - its seconds then measure real time, drains included) and a block INSIDE a running delay does
   not lengthen it. Exhaustion, the peak line, the curves and the speed re-target are unchanged (`AfterCharge` as before).
   Blows, kicks and bashes always restart it. `BlowOutcome.RestartedDelay` (default true; false when not charged) tells
   the module. `AthleticsRules`' constructor takes the switch LAST, default false (step 22) - the older tests' hand-built
   rules keep step 22's behaviour; `From` reads the setting.
3. **Logs**: the YOU block line (first of each kind per battle) ends `… once per blow, and the refill keeps running (only
   a WRONG-side block restarts its delay - OnlyWrongSideBlocksStopRefill)` / `… and restarts the refill delay (a
   WRONG-side block - a clean one would let the refill run)` / `(OnlyWrongSideBlocksStopRefill off: every paid block
   does)`; the verbose block lines get ` - the refill runs on`; the YOU-exhausted line says `refill starts 3.0 s after your
   last blow, kick, bash or WRONG-side block - a clean block or parry lets it run`; the settings sentence (mission start
   and summary) gains `; only a WRONG-side shield block restarts the refill delay - a right-side block or a parry costs but
   the refill keeps running (OnlyWrongSideBlocksStopRefill)` (or `every paid block restarts …` when off); the blocks
   summary line ends `| the refill delay restarted by W (right side, WRONG side, parries), the refill kept running under K
   (…) - OnlyWrongSideBlocksStopRefill (step 29)` (`AthleticsStats.AddBlockCharge(…, restartedDelay)`, per kind).

**The numbers - does a parrying exhausted man escape?** At the SHIPPED refill (flat, `RegenRateNearFullPercent` 100,
empty → full in 60 s at a walk) a pool of P points refills P/40 points per 1.5 s at a stand; a parry costs 2 × m, a
right-side shield block 1 × m (m = 1 soldier, 0.75 hero, 0.5625 hero party leader). Parrying a blow every 1.5 s he
climbs iff P > 80 m: a soldier needs a pool above 80 (Athletics skill > 80), a hero above 60, a hero who leads his party
always (the floor is 50). Right-side shield blocks: P > 40 m - everyone climbs. Examples: a 100-pool hero parrying nets
+1.0 per parry (+40 a minute: from empty to ~38 in the first minute - the unit test and the smoke); a floor (50) soldier
nets −0.75 per parry and stays at the bottom, but +0.25 per clean shield block. Fighting at more than walking pace
lowers the refill (×0.5 at a full run), so circling duellists gain less. On DESIGN's initial curve (50) the refill near
empty is ×1.39 faster (ln 2 / 30 s), so the threshold pools are ~28% lower. A wrong-side block every 1.5 s still holds
anyone at the delay.

**Tests / smoke**: Core 487 → 498 (`BlockRefillTests`: the rule's truth table on / off; a clean block / parry costs but
leaves the clock and the running refill alone, the next step refills; a wrong-side one restarts and holds; a block inside a
running delay does not lengthen it; off = every kind restarts and holds; the switch live through `TraxSettings` and its
place in the schema; the master switch / AthleticsEnabled = nothing charged, no clock touched; the hero parrying every
1.5 s from empty (+0.01 per cycle exactly, ~38 after a minute) vs step 22's 0 for good; the floor soldier (shield +0.25 a
block, parry stays at the bottom, a hero party leader +0.125); wrong-side blocks hold him at 0; the sentence and the
summary split). `AthleticsStatsTests`' expected texts gained the new clauses. Smoke 69 → 70 steps:
`BlocksLetTheRefillRun` (the real logic's `BlockTaken` every 1.5 s with `AthleticsMath.Regen` stepped in 0.05 s - the
regen pass itself needs native speeds; you as a 100-pool hero: 40 parries paid, the clock untouched, 35-40 of 100 after a
minute; the YOU line and the verbose tail; right-side blocks let it run; wrong-side blocks hold a man at 0 with 20
restarts counted; the switch off live = the delay restarts and the bar sinks, on again = it climbs; the master switch;
the summary split). The older steps pin the switch OFF in `AthleticsDefaults` (step 22's checks unchanged except the YOU
line's new tail).

**UNVERIFIED — only the game can tell (PLAYTEST D8 step 4, C's block list item 4)**
1. In a tournament, tired out then only blocking, the player's bar climbs; the opponent recovers while bracing → the
   blocks summary line shows most blocks under "the refill kept running" and the 1v1 ends in a fight.
2. Whether a soldier-sized pool parrying non-stop should also climb (today it sinks by design) - Anton's call; the levers
   are `CostPerWeaponParry`, `FullRegenSecondsStanding` and the curve.
3. Step 30 (tired AI blocks in a random direction) is the last part of the deadlock fix - more wrong-side blocks from tired
   AI, which then DO stop their refill.

## Step 30 — tired AI blocks in a random direction (research 2026-10-12; build below)

Anton (2026-10-12, the same tournament 1v1 as steps 28-29): at the exhausted end the AI blocked ~95% of his swings -
"crazy good AIs". His rule: the more exhausted an AI man is, the more his block DIRECTION is random. In the green (f = 1)
his own AI's choice; below it a mix with weight w = (TiredBlockRandomPercent / 100) × (1 − f) - at f 0.5 half his picks his
own, half a uniformly random direction (still right 1 in 4 by luck); at empty all random. Pairs with step 29: a wrong-side
shield block costs 5 and restarts his refill delay.

**Research (Part A) - verified in the v1.4.8 source, decompiled with ilspycmd 9.1 into %TEMP%\trax_decomp (RESEARCH §M)**
1. **How the guard direction travels.** Four bits of `Agent.MovementControlFlag`: DefendLeft 0x400, DefendRight 0x800,
   DefendUp 0x1000, DefendDown 0x2000 (`DefendDirMask` 0x3C00); DefendAuto 0x4000 / DefendBlock 0x8000 are set by no
   managed code. The player's guard is ONLY those bits: `MissionMainAgentController` ORs `GetDefendMovementFlag()` (native,
   the mouse) into `MovementFlags`, or with auto-block the MIRROR of the attacker's direction (`CustomBattleAutoBlockModel`:
   attacker AttackLeft → DefendRight, AttackRight → DefendLeft, Up → Up, Down → Down). The AI's input passes the same word
   through `OnAIInputSet` (step 16) - RTS Camera's attack-bit edits there act, and step 16's raised guard (DefendDown) is a
   DIRECTION (down / forward), not a direction-less "guard up". Whether the AI's own defend decision is in that word when our
   callback runs, or the native AI applies it after, is invisible from managed code: replacing the bits is plausible, UNVERIFIED.
2. **The game's own lever** (the find of this step): `DrivenProperty.AiRandomizedDefendDirectionChance` (40), set ONLY by
   `AgentStatCalculateModel.SetAiRelatedProperties` (Sandbox's, custom battle's and multiplayer's models call it) to
   **1 − num³**, num = clamp01(melee skill / 300 × 0.1 / 0.32 / 0.96 by difficulty) × `_AILevelMultiplier`; pushed to the
   engine by every `UpdateAgentProperties` - through our stat decorator (step 5). A recruit (skill 20, realistic): num 0.06,
   chance ≈ 1. **The tournament**: `TournamentBehavior` sets the multiplier to 1 + round / 3 (2.0 in the final): a 200-skill
   opponent has num 1.28 and a chance of −1.10 - NEVER random. That is Anton's "crazy good" opponent, in vanilla.
3. **Shields**: the defend direction is the shield's pose (`ArrangementOrder.GetShieldDirectionOfUnit`: a shield wall's front
   rank DefendDown, edges DefendLeft / Right, inner ranks DefendUp = overhead). `CorrectSideShieldBlock` is the engine's own
   judgement (native), read by the damage code for the shield's damage. Whether a wrong pose lets the blow past the shield or
   only makes a wrong-side block: native, UNVERIFIED. A weapon parry in the wrong direction is no parry - the blow lands.
4. **Riders**: the same bits and the same property (every human agent).
5. **Verdict: FEASIBLE - through the driven property, not the input bits.** The brief's design (replace his direction bits in
   OnAIInputSet, one roll per incoming attack, a timeout, hook every tired AI man) would work only if the native AI's decision
   is in the word when we see it, and costs a component + a native callback per tired man per frame. The property is the
   game's own per-decision randomiser: one float per recompute, no hook, no per-frame work, riders included, and it is exactly
   Anton's mix (below). The input route stays the documented fallback if PLAYTEST D8 shows the engine ignoring the property.

**The build (Claude's calls - Anton can overturn any)**
1. **The formula**: own = clamp01(the base model's value) (a tournament's −1.10 → 0); w = clamp01(TiredBlockRandomPercent /
   100) × (1 − f); the chance written = own + (1 − own) × w. P(a random pick) = w + (1 − w) × own - exactly "w of his picks
   random, the rest his own AI's (which itself randomises at its own chance)". w = 0 (green, setting 0, off, the player) →
   the property is NOT written: the game's value stands, negative or not.
2. **Applied in recompute steps**, like the speeds: `Fighter.BlockRandomWeight` = the w the decorator applies now, re-targeted
   with the other three multipliers (`AthleticsMath.SpeedUpdateNeeded`, 0.05 steps, any move to or from 0 at once) and applied
   by the same `UpdateAgentProperties` budget (≤ 50 a tick). Every settings change re-targets everyone (live).
3. **Never the player**: w = 0 for `Agent.IsMainAgent` (the hero you drive, also while RTS Camera's AI drives him) - the decorator
   checks it again. The player's direction is his mouse anyway. Riders: yes.
4. **One roll per incoming attack** is the native AI's own granularity (it decides a defence per perceived attack) - UNVERIFIED,
   nothing of ours runs per frame, so nothing can flicker. No timeout needed (no state of ours is held).
5. **Interplay with the forced guards** (step 16's timer hold, step 16's backpedal, step 23's brace): they raise DefendDown only
   when he holds no guard of his own (`OwnGuardKept` otherwise) - a fixed direction of OURS, which this step does not randomise.
   Whenever his own AI picks the direction - most blocks, held or not - the raised chance applies. Attack bits never touched.
6. **Gate**: ModEnabled first (`SpeedFactorsFor` returns nothing while it is off), then AthleticsEnabled, then the setting > 0 -
   switched off = everyone re-targeted to w 0 and recomputed back to the game's value over the next ticks.
7. **Measured** (summary, 2 lines; `[guard]` tag): the chance raised - men, recomputes, by f band his own avg → the written avg;
   the GUARD of AI men against melee blows by f band - blocked % (shield right side / WRONG side / weapon parry) vs landed %
   (kicks, bashes, horse charges, missiles left out) - Anton's playtest line: empty men should block far worse than fresh ones.

**Built (DONE 2026-10-12)** - file map in CLAUDE.md "Layout"
- Setting `TiredBlockRandomPercent` (Int 0-100, DESIGN initial 100, defaults.json 100; Tired fighters, right after
  `DamageBonusFollowsAthletics`; live). Settings 102 → 103. No config format bump (a missing key reads as its default and
  `ConfigStore` writes it in).
- Core `TiredBlock.cs` (TiredBlockRules, TiredBlockMath, TiredBlockStats), `Fighter.BlockRandomWeight`. Tests 498 → 514
  (`TiredBlockTests`).
- Module: `RetargetSpeed` (now an instance method - it asks `IsPlayer`) re-targets w beside the three multipliers
  (`SpeedUpdateNeeded(applied, w, 0)`: 0.05 steps, any move to or from 0 at once) and records f
  (`TrackedAgent.BlockRandomPeakShare`); `SpeedFactorsFor` hands w out (gated ModEnabled → AthleticsEnabled as before);
  `TraxAgentStatModel.UpdateAgentStats` writes `TiredBlockMath.Chance(the base's value, w)` when w > 0 and the agent is not
  the main agent, then `NoteTiredBlock`. `AthleticsLogic.TiredBlock.cs`: the [guard] lines (mission start, a settings
  change, the first raised chance in full, verbose `guard-chance`), `TiredGuardHit` in OnMeleeHit BEFORE `BlockHitTaken`
  (f before the block's charge) → `GuardTaken`, the 2 [summary] lines.
- Base models recompute the chance on every update (Sandbox's `UpdateHumanStats` and custom battle's both call
  `SetAiRelatedProperties`) - so our write never compounds (the smoke checks two recomputes in a row). A third-party base
  model that does NOT recompute it would make it creep up per recompute; none known.
- Smoke 70 → 72 steps (`Program.TiredBlock.cs`): `TiredBlockDirection`, `TiredBlockCostStaysFlat` (1000 tired men, each a
  block + a recompute through the real decorator: ~0.1 µs a blow, 0 bytes). The older steps pin
  `TiredBlockRandomPercent` 0 in `AthleticsDefaults` (they count recomputes).

**The brief's items that the property route makes moot** (written down so nobody rebuilds them): the direction ↔ bit
mapping (no bits are written - the smoke checks DrivenProperty 40 instead), the one-roll-per-attack bookkeeping and its
timeout (the native AI rolls per decision, nothing of ours is held), hooking tired men lazily (no hook), the replaced /
kept / right-by-luck counts per decision (invisible from managed code - the summary shows the chance written by band and
the guard OUTCOME by band instead).

**The fallback, designed, not built** (if PLAYTEST D8 shows the engine ignoring the property): step 16's `AiInputComponent`
on every AI man below the peak line (hooked lazily from the tick, unhooked when back in the green, the RTS Camera rule
kept); in `OnAIInputSet`, when `flags & DefendDirMask` shows a NEW own direction (or a change of it), ONE roll with
probability w → a uniformly random one of the four bits, held while his own pick stays the same, dropped when he stops
defending or after a ~1.5 s plumbing timeout; a forced `DefendDown` of ours is not rolled; attack bits never touched.

**UNVERIFIED - only the game can tell (PLAYTEST D8 step 5, L6e)**
1. The native AI reads the chance per defence decision and a raised chance makes it pick wrong → the `AI guard by f` line:
   the empty band's blocked % well below the peak band's, `WRONG side` and `landed` up. If flat: the fallback above.
2. A random pick is uniform over the four directions (native) - only the outcome shows it.
3. A wrong pose with a shield → a wrong-side block (`WRONG side` count up) rather than a clean miss of the shield - either
   way the blow is worse for him.
4. The tournament opponent: `tired block direction … N of them over a game value below 0` > 0 in late rounds.


## Step 32 — the swing stack: stored swings, a pause only at 0 (DONE 2026-10-12)

Anton (2026-10-12, the tournament 1v1 again): below the peak line every attack was followed by its pause, so a tired man swung
once every ~3 s - one telegraphed swing, blocked ~95% - and kicks (never held) were the way round it. His design, "chain
swings stack": each fighter STORES swings, cap = max(`SwingStackMinimum` 2, round(current points / `SwingStackPointsPerSwing`
20)) half away from zero; an attack spends one and waits only at 0, exactly until the next is back; one comes back per the
pause that attack would have left (D × (1/m − 1) at his CURRENT m, + the AI's battle-pace share) while he is not attacking -
the average rate unchanged, bursts possible; kicks and bashes spend one too and cannot start at 0.

**Research - the kick input (v1.4.8, %TEMP%\trax_decomp)**
1. **Your kick** is NOT an attack bit: `MissionMainAgentController` (View, ~line 772) sets `EventControlFlags |= 0x8000`
   (`Agent.EventControlFlag.Kick`) when game key 16 ("Kick", default E) is PRESSED that frame and `mainAgent.KickClear()` (native)
   or he is mounted. A one-frame event, written in the same pre-tick our gate already follows (step 13's `PlayerAttackGate`, first
   in the behaviour list). So the gate can take it out the same way it takes out the attack bits: clear 0x8000 from
   `EventControlFlags` before the engine reads them. **Clean** - no patch. Only on foot (a mounted "kick" is set without
   `KickClear` and riders never kick - step 18). The gate runs only while your hold is on, which is exactly "no stored swing".
2. **A shield bash** is the attack button while blocking (step 18) - the attack bits: held with every attack at 0, no extra work.
3. **The AI's kick**: `AgentComponent.OnAIInputSet(ref EventControlFlag, ref MovementControlFlag, ref Vec2)` hands us the AI's
   EVENT flags too (step 16's hook) - so a man held BY INPUT at 0 has 0x8000 taken out (`AiInputState.HoldKicks`,
   `AiInputHook.HoldKick`). Whether the native AI's kick decision is in that word when we see it is UNVERIFIED (the AI rarely
   kicks - step 18 saw none); the summary counts the frames cleared and any kick that started at 0 anyway. NoAttack (the old
   technique) does what it does - not touched.

**The build (Claude's calls - Anton can overturn any; DESIGN §2 "The swing stack", decision 30)**
1. **Core `SwingStack.cs`**: `SwingStackRules` (live; Enabled = ModEnabled && AthleticsEnabled && SwingStackEnabled, KicksOn,
   OffBecause, Describe), `SwingStackMath` (RoundAway, Cap from points / from a Fighter, Full = 2^20 "full until a cap clamps it",
   FallbackSwingSeconds 0.8 - plumbing, the guide's swing until his first measured attack - burst bins, CountText "4/8"),
   `SwingStack` (per fighter, allocated once: Stored, Progress, ClockAt, GraceUntil, Run, Waited; Advance(now, refill, cap,
   attacking) - the clock stops while attacking, a refill under 0.1 s fills it at once; Spend(cap) → FromStack / AfterWaiting /
   AtEmpty (an attack at 0 loses the refill so far - never free); Clamp; ProgressAt (the bar's projection, no mutation);
   SecondsToNext; StartGrace / EndGrace; FillUp; CloseRun), `SwingStackStats` + 5 [summary] lines.
   `PaceNotHeld.FromStack` (the AI timer's "not held" line). `PlayerAttackTimer.AttackEnded(…, pauseOverride)`,
   `StartCountdown`, `Retarget` (the countdown follows the stack). `AttackRecoveryReading.WithStack` (StackOn, Stored, Cap,
   StackProgress; Share / Recovering follow the stack).
2. **The clock** (`AthleticsLogic.SwingStack.cs`): advanced at every channel-1 action change BEFORE the phases move (so the
   interval is filed in the state he was in), in every regen pass (0.1 s, everyone - also the fill sample by f band), every tick
   for an AI man waiting at 0 (TickPace) and every gate frame for you. "Attacking" = PhaseKind WindUp or Release, or the reload
   after a loose (AwaitReloadEnd); a block recoil, the guard, the pause = rest. Refill seconds: you - Pause(D, m); the AI -
   `BattlePaceMath.Plan(bp, his class, D, m).Total`; D = `TrackedAgent.StackD` (his last measured attack, set at AttackEnded).
3. **Spends**: ObserveAction's ReleaseMelee / ReleaseRanged / ReleaseThrowing, before the charge and before
   PlayerAttackStarting; `ChargeKickOrBash` first thing (whatever it costs, KicksUseSwings). `StackClamp` in AfterCharge.
4. **The AI** (`AiAttackEnded` → `AskStackPause`): a swing stored = no pause (`FromStack`) and, after a MELEE attack, the
   clock waits `AiMeleeGapSeconds` (StartGrace) - **the AI's own gap is not rest**: step 21 measured that the AI waits ~1.0 s of
   its own after each attack and a pause ADDS to it; without the grace the stack refills inside his natural rhythm (a fresh
   shield man's 0.76 s share < his 1.0 s gap: never held, battle pace undone; a tired man ~37% more attacks). With it, at 0 the
   cycle is D + P + G (step 13's), bursts are D + G, and idle time beyond G refills. At 0: EndGrace, the wait = SecondsToNext →
   `AskPause(…, stackWait)` (PaceState.StackWait; no FullStrength / NoDuration refusals - the wait does not need this attack's D);
   TickPace ends a StackWait hold when `StackWaitOver` (the clock moved, a swing back → MarkWaited) - Until is only the estimate
   (the stack switched off mid-wait = the estimate stands). StartHold by input sets `HoldKicks`; EndHold sums `KicksCleared`.
   A kick that spends an AI man's last swing → `AskStackWaitAfterKick` (the same wait).
5. **You**: `PlayerAttackStarting` - with a swing still stored after the spend, NO hold (the burst may chain); at 0 the hold
   from the release's start (expected = the wait). `PlayerAttackEnded` - stored = no countdown (an early hold released); 0 = the
   countdown = the wait (`_playerStackWait`). `GateFrame` retargets it first every frame (`RetargetPlayerWait`: back = end now,
   so a held button fires this frame). `GatePlayerInput` → `KickGate` (managed; the smoke drives it): the kick bit out at 0, on
   foot, counted, logged. `PlayerCountdownEnded` counts the wait and marks the next spend "after waiting". `TickPlayerTimer`:
   the retarget + a safety net (an empty stack with no hold - a kick spent the last - starts the countdown).
   `TryGetPlayerRecovery` adds the stack (count, cap, `ProgressAt` now).
6. **Fresh starts**: `ResetPhases` (now an instance method - the switch off / on, the hideout's boss fight) FillUp + counts the
   burst it cuts; `NoteStackSettings` (SwingStackEnabled on mid-battle = everyone full; off = the line names the switch).
7. **The bar**: `AttackRecoveryVM.LabelText` bound by the prefab's Label (`Text="@LabelText"`), rebuilt only when the count or
   the cap moves (`SetCount` - no per-frame string): "Swings 4/8" / "Attack recovery" with the stack off. Fill = the refill
   toward the next (1 at the cap), amber below the cap, seconds only while waiting at 0. Chosen over pips: one TextWidget, a
   binding the smoke's prefab check already validates, any cap (2-30+) fits; pips would need a list prefab and a cap limit.
8. **Logs**: `[rate] swing stack (step 32) at mission start: …` (Describe), switch / settings lines; YOU (bucket
   `athletics-stack`): first burst, stack empty (first in full: "1 → 0 of 4 on 70 points … the next comes back in X s"), a kick
   refused (first in full), an attack at an empty stack; `[hud] recovery bar: first empty swing stack shown`; the first-pause
   line names the stack; [summary] `swing stack:` + you / AI (attacks from the stack / after waiting / at empty, bursts by
   length, waits) + kicks + the fill by f; `attack rate - AI timer, not held: … a stored swing left (step 32's swing stack) N`;
   the recovery bar's summary clause "swing stack shown, fewest stored N".
9. **The attack-rate verdicts**: unchanged math (each band's average cycle vs the target ÷ m). With the stack a band can hold
   mostly burst cycles ("too fast") and the next band the waits ("too slow"); over a fight the average holds. Read with the
   swing stack lines, or A/B with SwingStackEnabled off.

**Built (DONE 2026-10-12)** - file map in CLAUDE.md "Layout"
- Settings 103 → 107: `SwingStackEnabled` (true), `SwingStackPointsPerSwing` (Int 5-100, 20), `SwingStackMinimum` (Int 1-10, 2),
  `KicksUseSwings` (true) - Tired fighters group, after `AttackAnimationMinPercent`; live. No config format bump.
- Tests 514 → 531 (`SwingStackTests` +17; AttackRateTests' not-held line). Smoke 72 → 75 steps: `SwingStackThroughTheLogic`,
  `SwingStackCostStaysFlat` (1000 men waiting at 0: the AI timer's tick 0.087 ms, the stack pass 0.035 ms, 0 bytes),
  `RecoveryBarShowsTheStack`; the older steps pin the stack OFF (`AthleticsDefaults` → `SwingStackOff`).

**Playtest checks (PLAYTEST part H)**
1. Your burst: rest, then hammer - "Swings N/M" counts down with no wait; the YOU lines `first burst` and `swing stack empty`.
2. At 0 the kick key does nothing (`kick pressed … refused`); the summary's `refused at 0 - yours N`.
3. The AI: `attack rate - AI timer, not held: … a stored swing left` high, `swing stack - AI: … at an empty stack` ~0 (else
   chained blows), `refused at 0 … the AI's N input frames` (0 = the AI does not kick through our hook - unverified).
4. Battle pace still bites: the `battle pace` lines' attacks a minute per man about as before (the grace) - if the AI swings
   clearly more often, tell Claude (the gap grace may be too short).

## Step 33 — the sprint: a short burst of speed, a dodge for you, sharper charges for the AI (DONE 2026-10-12; the research below was written before coding)

Anton (2026-10-12): a sprint store in seconds = current Athletics points ÷ `SprintPointsPerSecond` (20) - 150 points = 7.5 s,
empty = 0; refill (not sprinting) empty → full in `SprintRefillSeconds` (7) standing at / above the peak line, × 0.5 moving
(above a walk), × a tiredness line 1 at the peak line → 0.5 empty, the two multiply; start only with ≥ 20% of it stored; the
speed: the first 30% of the sprint's length T (= the seconds stored at its start) at 1.6 × his CURRENT run speed, then a
straight line to 1.1 × at T; the store drains 1 s a second only while moving; releasing the key ends it; no Athletics refill
while sprinting; on foot only. The AI sprints once on a NEW movement order: charge up to 100% of its store, advance / move up
to 50%. Research first, STOP if it needs native patching.

**Research - verdict: feasible, no patch** (RESEARCH §N has the sources)
1. **Speed above the base**: `MaxSpeedMultiplier` is the base models' whole speed factor (0.63-0.79 for typical troops, 0.91
   at skill 300, × perks and a banner on top) - not a 0..1 share; no managed clamp; the engine's top follows it linearly
   (Anton's own 2026-10-12 summaries: engine x0.81 / asked x0.81, x0.31 / x0.30). 1.6 × 0.63 = 1.0 sits inside vanilla's own
   range; a skill-300 hero at 1.46 is past it. UNVERIFIED above ~1.0: a native cap, the feet sliding - the new `sprint speed
   check` summary line answers the first. The decorator multiplies the run factor it already applies (run × sprint) - one
   lever, nothing new on the engine side. Acceleration (`TopSpeedReachDuration` 2.5-3.6 s) untouched: from a standstill the
   sprint ramps up like any run; from a run it is immediate.
2. **The key**: Left Ctrl is bound in v1.4.8 only to the campaign panels' "entire stack" modifier (party / inventory screens)
   - Anton's own BannerlordGameKeys.xml confirms; Left Shift is Zoom, Z crouch (stealth), Caps Lock walk, Left Alt the
   markers. MissionScreen reads Ctrl only in the free camera (photo mode, deployment) and cheat mode; the order menu's
   formation drag uses Ctrl for a horizontal layout (the sprint is off while the order menu is open). **Default
   LeftControl.** The key is `SprintKey`, a key NAME from a fixed list (Core `KeyNames`, each checked against the game's
   `InputKey` enum by the smoke), read live through `Mission.InputManager.IsKeyDown(InputKey)` - the scene layer's input
   context, which already answers false while a menu holds the keys.
3. **Order changes**: poll, not the event. `Formation.OnBeforeMovementOrderApplied` fires on every SetMovementOrder (the AI
   re-applies a moving Move on its ticks) before the "practically the same" check; `OrderController.OnOrderIssued` misses the
   formation AI's own behaviour changes. Polling every formation's order KIND (step 23's read, the AI's advance / tactical
   charge on a Move) every 0.25 s is ≤ ~80 managed reads - and gives exactly "the kind changed".
4. **AI in formation**: a formation man's speed is capped as a multiplier of his own top, against the formation's speed
   (the average of its men's tops, cached ~2 s). So the whole formation sprinting together is what makes it faster - one man
   alone would be held to the others' pace; the charge's last stretch (closer than 2 × top speed to the enemy formation) runs
   uncapped. That is why the AI sprint takes the whole formation at once.

**The build (Claude's calls - Anton can overturn any; DESIGN §2 "The sprint", decision 31)**
1. **Core `Sprint.cs`**: `SprintRules` (live; Enabled = ModEnabled && AthleticsEnabled && SprintEnabled - the store is made
   of Athletics points; AiOn; Describe(key)), `SprintMath` (Capacity = points / per second, TiredFactor = E + (1 − E) f,
   RefillMultiplier (moving × tired), RefillPerSecond = capacity / T × it, SecondsToFull, CanStart (≥ 20% and a store at
   all), AiShare, Group (step 23's BraceOrderKind → charge / advance-or-move / none), Multiplier (max for the first share of
   the length, then a straight line to min; min capped at max), Quantize (0.05 steps), SecondsText (tenths rounded DOWN -
   the bar never shows more than there is), the bar's colours; plumbing: moving above 0.5 m/s, melee 3 m, melee check and
   order poll 0.25 s), `SprintStore` (per fighter, allocated once: Stored - +∞ = full until the first Clamp -, Active,
   Length, Elapsed (seconds of MOVING), Since, StoredAtStart, Order, ByKey; Clamp, FillUp, Refill (never while active),
   TryStart (share of the store; the gate on the whole store), Advance (drain = the clock, only moving; ends StoreEmpty /
   LengthDone), Stop), `SprintReading` (the bar), `SprintStats` + 4 [summary] lines.
2. **The key setting type** - `ParamType.Key` + `KeyNames` (22 names, the game's `InputKey` names; matched ignoring case,
   spaces, '-', '_', and "ctrl"/"lctrl" short forms). Stored as the index (the double array stays), written / read as the
   NAME in config.json and defaults.json (`FormatJson`, ConfigFile.TryConvert, DefaultsFile.TryValue - a number is refused
   there), an MCM TEXT BOX (`AddText`, ProxyRef<string>; an unknown name refused with one [mcm] line, the old key kept), the
   preset passes the name. Chosen over a free string: the whole settings stack is doubles, and a fixed list keeps a fight
   key (W, Space, E…) from being picked by mistake. The smoke checks every name parses as the game's InputKey.
3. **The lever**: `TrackedAgent.SprintMultiplier` (applied, quantised); `SpeedFactorsFor` returns run × it, so the
   decorator's existing `ScaleRun` puts `MaxSpeedMultiplier` above 1 - no new engine call. A step sets SpeedDirty → the
   tick's `UpdateAgentProperties` loop (50 a tick). That loop's start now ROTATES (`_budgetStart` = the first man left
   waiting), so a 1000-man charge (1000 marked at once, then ~11 steps each over the sprint) is queued over ~20 ticks and the
   end of the list never starves. The run-speed check skips a sprinter's samples (and any sample while SpeedDirty); the
   walk/run sampling skips sprinters.
4. **The tick** (`AthleticsLogic.Sprint.cs`, from TickAthletics before the recompute loop): switched off → EndAllSprints +
   ForgetOrders; your key every tick (`ISprintBody.KeyDown(SprintKeyIndex)` = `Mission.InputManager.IsKeyDown(InputKey)` +
   `PlayerCanSprint` = a fight mode and the order menu closed + YouDrive): a PRESS starts (refusals: below 20%, no store,
   a horse - the first in full, then rate-limited), letting go = Released, the menu / not your hands = ControlChanged;
   after the master switch a still-held key waits for a new press. The AI: `PollOrders` every 0.25 s - every formation of
   every team (≤ 8 teams × 10) through `ReadOrders` (step 23's `GameBraceBody.KindOf`, now a shared static); the first look
   only records; a changed kind marks the slot, then ONE pass over the fighters starts the men in marked slots
   (`OrderChangedFor`: not the player / not his own AI = skipped, a running AI sprint ends OrderChanged, share 0 = none, a
   horse = skipped). The 0.1 s `SprintPass`: Clamp to his points (counted), RunSprint (left the field, mounted, control
   changed, the speed sample, Advance, the melee look every 0.25 s, the multiplier) or Refill (native reads only for a store
   below full). RegenPass skips a sprinter before any read (no Athletics refill). Ends on every path: Released, StoreEmpty,
   LengthDone, InMelee, OrderChanged, Mounted, SwitchedOff (all / AI only), LeftField (Forget), ControlChanged, FreshStart
   (step 19's per-man fresh start also fills the store), MissionEnd (CloseSprints before the summary), Error.
5. **The speed check** (the research's open question): at a sprint's start, with nothing pending, his top speed is the base;
   every pass while the asked multiplier is in his properties (not SpeedDirty) files asked = sprint × run ÷ the run at the
   start against engine = top ÷ base. "follows" = means within 0.05 over 20+ samples.
6. **The bar**: `SprintBarView` + `SprintBarVM` + `TraxSprintBar.xml` - the recovery bar's recipe (label, frame, a
   FillBarWidget, a 2 px white mark at `MarkOffset` = inner width × the start share). Above the recovery bar
   (`SprintBarOffsetAbove` 46 = 24 + 22), as long and thick as it (reusing RecoveryBarWidth / Height - one new number
   only). Fights only (no OutsideToggle). Shown with ShowPlayerBar + SprintEnabled; its own switch ShowSprintBar.
7. **Logs**: `[sprint]` mission start (Describe), settings changes, switch-offs with counts; YOU first sprint in full (stored,
   T, the profile, the run property before → after), later starts / ends (bucket `sprint-you`), the first refusal in full;
   the first new order and the first AI order sprint in full; verbose `sprint-order` / `sprint-ai` through VerboseWants;
   `[hud] sprint bar` first values / first sprint; [summary] 4 sprint lines + the bar's hud line.

**Built (DONE 2026-10-12)** - file map in CLAUDE.md "Layout"
- Settings 107 → 122: a "Sprint" group (13 groups, after the brace): SprintEnabled, SprintKey (LeftControl), SprintPointsPerSecond
  20, SprintMinStartPercent 20, SprintMaxMultiplier 1.6, SprintMinMultiplier 1.1, SprintFullSpeedSharePercent 30,
  SprintRefillSeconds 7, SprintRefillMovingFactor 0.5, SprintRefillExhaustedFactor 0.5, SprintAiEnabled, SprintAiChargeSharePercent
  100, SprintAiMoveSharePercent 50; ShowSprintBar (Your Athletics bar); SprintBarOffsetAbove 46 (Advanced). No config format bump
  (new keys come in with their defaults).
- Tests 531 → 550 (`SprintTests` +19). Smoke 75 → 80 steps: `UseFakeSprintBody` (the key names = InputKey), `SprintThroughTheLogic`,
  `SprintCostStaysFlat` (1000 men on a charge: 1000 marked = 20 ticks of the budget, ~12 recomputes a man, 0.006 ms a tick,
  0 bytes), `SprintPrefabIsValid`, `SprintBarThroughTheView`; the MCM steps check the text box (a loose name sets it, "Banana" is
  refused and logged); the older steps pin the sprint OFF (`AthleticsDefaults` → `SprintOff`).

**Playtest checks (PLAYTEST D10, appendix L6g)**
1. The speed check: `engine top avg` ≈ `asked avg` (≈ x1.4) - else the engine caps the run property above its base: tell Claude.
2. The feet at x1.6 (sliding?), a big-skill hero's top speed (too silly?), Ctrl doing anything else in a fight.
3. An AI charge: `sprints - charge N men` after `new orders seen - charge`; the formation reaching the enemy sooner (the formation's
   speed cache refreshes every ~2 s - a move / advance sprint may show only after it).
4. `[error] sprint.*` none.


## Step 34 — the AI guard log split by a shield in hand (DONE 2026-10-12)

Anton (2026-10-12, his tournament log): tired AI with SHIELDS turned step 30's random direction into WRONG-side shield
blocks (63% at empty) - and the shield still stopped the blow (landed 6%). Whether tired SHIELDLESS men parry worse (a
wrong-direction parry is no parry - the blow should land) could not be told: `AI guard by f` mixed the two. Pure logging,
no gameplay change, no new setting (none needed: the split is always written - three summary lines, no per-hit line).

**Built**
- Core `TiredBlockStats`: one private `Guard` table (blows, shield right / WRONG side / parry / landed by f band) three
  times - all, WITH a shield in hand, WITHOUT; `AddGuard(bin, kind, shield)`; `AddRaised(bin, game, chance, shield)` +
  `RecomputesWithShield`; accessors `Blows / Landed / WrongSide / Parried(bin, shield)`. Summary 2 → 4 lines: the raised
  chance (now "N recomputes - X with a shield in hand, Y without; …"), the old `AI guard by f` line UNCHANGED (all - the
  totals stay comparable with older logs), then `AI guard by f WITH a shield in hand (when the blow came - step 34): …
  (a wrong direction with a shield = a WRONG-side block: the shield still stops the blow)` and `AI guard by f WITHOUT a
  shield (when the blow came - step 34): … (a wrong direction without a shield = the blow lands: empty should land far
  more than peak)`. Fixed arrays, nothing allocated per blow.
- Module `AthleticsLogic.TiredBlock.cs`: `ShieldInHand(st, site)` = step 21's `IWeaponFacts.Read(st, melee: true, …)`
  (the off-hand item's usage IsShield - a pointer read + the managed item: safe in OnMeleeHit and in the stat decorator);
  read in `GuardTaken` AFTER its early returns (you / Athletics off: no read), i.e. where step 30 counts - in OnMeleeHit
  BEFORE `BlockHitTaken` charges the block; and in `TiredBlockApplied` (the recompute). A failing read: one `[error]`
  per site (`guard.shield`, `guard.shield.recompute`), counted "without" - the totals stay whole.
- "With a shield" = in his OFF HAND when the blow arrives - a shield on his back (step 23 puts it away / takes it out)
  counts "without", which is right: it cannot block a frontal blow. A blow at the shield on his back is still "landed"
  in both tables as step 30 counted it.
- Tests 550 → 551 (the split adds up; the two new lines' exact text; the empty-table texts). Smoke stays 80 steps:
  `TiredBlockDirection` extended - the stand-in weapon read per agent, a shield dropped mid-fight moves the next blows
  to "without", no read for you / Athletics off, the recomputes counted with a shield, the 2 new lines verbatim.

**What to read in a log (PLAYTEST D8 step 5, L6e)**: the WITHOUT line first. Its empty band's `landed` well above its
peak band's = tired shieldless men parry worse (step 30 works for them). Flat with a few dozen blows per band = the
engine's random direction does not reach parries (then step 30's input fallback is the next idea). The WITH line: a
rising `WRONG side` with `landed` flat = the shield still saves him - by design of the engine (UNVERIFIED item 3 of step
30 answered by Anton's log: a wrong pose is a wrong-side block, not a miss of the shield).

## Step 35 — sprint tuning: the peak on the fresh run, a 2 s floor, 1 s acceleration (DONE 2026-10-12)

Anton (2026-10-12, after two tournaments): the sprint felt very slow; he raised `SprintMaxMultiplier` to 2.0 in MCM. His log:
`sprint speed check: asked x1.66, engine x1.66, max x2.00` - the engine DOES reach the asked top (no cap). What was slow:
(1) **acceleration** - the game's foot `TopSpeedReachDuration` is longer than the sprint's full-speed part, and his taps were
0.4-0.8 s; (2) **tired** - the store collapsed (0.07 s stored = points / 20 near empty) and 2 × a tired run (× 0.3) is still
slow (0.6 of a fresh pace).

**The game's acceleration (verified in the v1.4.8 decompile)**
- `DrivenProperty.TopSpeedReachDuration = 86`, a per-agent value in `AgentDrivenProperties` (the managed `_statValues` array,
  sent with the rest by UpdateAgentProperties). Sandbox `UpdateHumanStats` assigns it with `=` on every recompute:
  `2.5 + max(5 − (1 + effectiveSkill × 0.01), 1) / 3.5 − min(weight / (weight + gear), 0.8)` (skill = Athletics; weight /
  gear from the character and his equipment) - **typically 2.3-2.8 s on foot**; CustomBattle: `2 / max((200 + skill) / 300 ×
  weight / (weight + gear), 0.3)`; a horse's is the RidingModel's acceleration (on the mount agent - untouched). So the stat
  decorator - where `MaxSpeedMultiplier` is already written, after the base model - is the right place: BaseModel first
  writes the game's value fresh, we lower it only while he sprints, and the next recompute without a sprint gives the base
  value back with no restore code.
- UNVERIFIED: how the engine applies it to a man already running (a sprint from a run should be immediate anyway); whether a
  1 s reach looks like a jolt - PLAYTEST D10 step 2.

**The build (Claude's calls; DESIGN decision 32)**
1. **The peak on the FRESH run** (Anton's call). Core `SprintMath.RunFactor(s, elapsed, length, run)` = the factor written
   on the run property, × his FRESH run, IN PLACE OF his tiredness run multiplier `run`: `SprintMaxMultiplier` for the first
   `SprintFullSpeedSharePercent` of T, then a straight line to `SprintMinMultiplier × run` at T. Fresh 2.0 → 1.1, half way
   to the peak line (run 0.65) 2.0 → 0.715, exhausted (run 0.3) 2.0 → 0.33. `Multiplier` (step 33's) = RunFactor at run 1 -
   a fresh man's profile is unchanged. `TrackedAgent.SprintRun` (0 = none) replaces step 33's `SprintMultiplier`;
   `SpeedFactorsFor` returns it as `run` while set. `Quantize` keeps 0.05 steps but floors at 0.05, not 1 (a tired fade
   ends below 1). Recomputes: the pass steps the factor at most once per 0.1 s, so even an exhausted man's 2.0 → 0.35 (33
   steps of 0.05) costs ≤ ~16 recomputes over his 2 s sprint (the 0.1 s pass is the bound); the cost smoke (fresh men, step
   33's numbers) stays ~12 a man.
2. **`SprintMaxMultiplier` 2.0 in defaults.json** (DESIGN's Default column keeps the initial 1.6). Its range 1-3 already
   leaves room above 2.0 - not changed. No config migration (Anton's config.json already holds 2.0 from MCM).
3. **The floor `SprintMinSeconds` (2, range 0-5)**: `Capacity = max(floor, points / pps)`; `PointsCapacity` (no floor),
   `OnFloor` (the floor holds it up). The refill rule is unchanged: `RefillPerSecond = capacity / SprintRefillSeconds ×
   moving × tired`, so the whole CURRENT store (2 s on the floor) still takes `SprintRefillSeconds` ÷ the factors - 14 s
   standing at an empty bar, 28 s moving. `TryStart`: `NoStore` only when the capacity is 0 (floor 0 and points 0); a
   used-up store with a capacity is `BelowMinimum` (the 20% gate on the capacity as before) - so with a floor an empty
   man sprints his 2 s once it has refilled past 0.4 s.
4. **`SprintTopSpeedReachSeconds` (1, range 0-5, 0 = the game's; below 0.1 counts as 0.1)**: `SpeedFactorsFor` gains the
   base model's value in (`gameReach` - remembered as `TrackedAgent.GameReach` on every recompute of a tracked man) and
   `out reach` (the setting while `SprintRun` > 0); the decorator writes `SpeedPenalty.CapTopSpeedReach` = `SprintMath
   .TopSpeedReach(game, ours)` = min(game, ours). A recompute happens at the sprint's start (SprintRun 0 → x), every 0.05
   step, its end (x → 0), and - new - on any sprint setting change mid-mission (`NoteSprintSettings` marks every sprinter
   SpeedDirty), so a reach change applies at once rather than at the next step.
5. **Logs**: mission start / settings line - `never below 2.0 s (an empty bar still holds 2.0 s)`, `speed x2.00 of his
   FRESH run speed (whatever his Athletics) … then a straight line to x1.10 of his CURRENT run speed`, `top speed reached
   in at most 1.0 s while sprinting (the game's own acceleration: ~2.3-2.8 s on foot)`; the first YOU sprint: the floor
   (`0 Athletics points / 20.0 = 0.00 s - the store's floor SprintMinSeconds 2.0 s holds it up`), the run property before
   → after (`x0.30 → x2.00`), the fade target (`x1.10 of your current run (x0.33)`), the acceleration (`top speed reached
   in 1.00 s instead of the game's 2.60 s`); the first AI sprint the same; later YOU starts add `(the floor)` and the
   factors. Summary: `sprint - you: … N started off the 2.0 s floor`, `sprint - AI: … off the 2.0 s floor N`, the speed
   check now `x his FRESH run speed = his top at the sprint's start ÷ his run multiplier then` (asked = SprintRun, engine =
   top ÷ the start's top × the start's run multiplier - meaningful for a tired man too: his peak reads x2.00) + `acceleration
   while sprinting: the game's top-speed reach avg … s → ours … s (sprint starts: N)`. Still 4 lines.
6. **Tests 551 → 557** (SprintTests +6: the fresh-run profile, the floor, its refill, the acceleration cap, live settings +
   the master switch + Describe, the summary clauses; the helper's new args default 0 so step 33's tests stay step 33's).
   **Smoke 80 → 81**: `SprintTuningThroughTheLogic` (an exhausted you through the real logic AND the real decorator - the
   run property 0.8 × 2.0 at the start, not 0.8 × 0.3 × 2.0; x2.0 held to 0.6 s, the fade towards 0.33; TopSpeedReachDuration
   2.6 → 1.0 and back to 2.6 after; the floor's refill 2 / 7 × 0.5 a second; reach 0 = the game's, a change mid-sprint
   recomputes him; the master switch = the base values; an exhausted AI man off the floor; SprintMinSeconds 0 = refused
   (live); the lines). Step 33's smoke checks PIN `SprintMinSeconds` 0 / `SprintTopSpeedReachSeconds` 0 in `SprintDefaults`
   (they test step 33's store); its one tired check now asks the new rule (run x0.85 → still 0.8 × 1.6). `FreshStatsModel`
   writes TopSpeedReachDuration 2.6.

**Numbers (defaults: pps 20, floor 2 s, peak 2.0, end 1.1, 30%, run floor 0.3, peak line 75%; × a fresh run)**

| Bar | Store, pool 100 | Run now | Peak | End | Refill empty → full standing / running |
|---|---:|---:|---:|---:|---:|
| 100% | 5 s | 1.00 | 2.00 | 1.10 | 7 / 14 s |
| 75% | 3.75 s | 1.00 | 2.00 | 1.10 | 7 / 14 s |
| 50% | 2.5 s | 0.77 | 2.00 | 0.84 | 8.4 / 16.8 s |
| 25% | 2 s (floor) | 0.53 | 2.00 | 0.59 | 10.5 / 21 s |
| 0% | 2 s (floor) | 0.30 | 2.00 | 0.33 | 14 / 28 s |

**Playtest checks (PLAYTEST D10, L6g)**: a 0.5 s TAP should lunge at once; an empty bar shows "Sprint 2.0 s" and bursts as
fast as a fresh man then fades to the tired jog; the summary's acceleration clause (the game's 2.3-2.8 s → ours 1.00 s);
`engine top` ≈ `asked` (both × the fresh run); the feet at 2 × (sliding?) and the 1 s reach (a jolt?).

## Step 36 — the AI sprint by order: a use and a keep per order kind (DONE 2026-10-12)

**Anton (2026-10-12):** "I will position my shock troops behind enemies and they won't use up all their sprint before even
getting the real charge command." Step 33 sprinted an AI man on every NEW order: a charge up to 100% of what he had
stored, an advance OR a move up to 50%. Three moves round the enemy's flank emptied the store before the charge.

**The rule.** Each order kind has a USE and a KEEP, both % of his FULL store (the capacity now: current points ÷
`SprintPointsPerSecond`, never below `SprintMinSeconds`); he sprints `min(use x cap, stored − keep x cap)` seconds
(`SprintMath.AiAmount`), nothing below `SprintMath.AiMinSeconds` 0.2 s (plumbing - a twitch is not a sprint).
Defaults (Anton's): charge 100 / 0, advance 50 / 0, move 20 / 70. A 5 s store: move from 100% → 1 s (80% left), from 80%
→ 0.5 s (70% left), from 60% → none; advance from 100% → 2.5 s, from 60% → 2.5 s (10% left); charge → all of it.

**Which kinds** (`SprintMath.Group` over step 23's `BraceOrderKind`, read by `GameBraceBody.KindOf`):
- Charge: `Charge`, `ChargeToTarget`, `AttackEntity` (attack a gate / a siege engine), `AiCharge` (an AI Move under
  `BehaviorTacticalCharge`).
- Advance: `Advance` (F1 F4 - Anton's "engage"), `AiAdvance` (an AI Move under `BehaviorAdvance` / `BehaviorCautiousAdvance`
  / `BehaviorVanguard`).
- Move: `Move` - the game's `MovementOrderEnum.Move` that is neither of the above: F1 F1 (move to a position) and an AI
  general's Move under defend / skirmish / flank / the rest. That is the shock-troop case for the player; for the AI
  general it means its repositioning dashes are short and keep a reserve too, which reads right.
- None: `Stop` (hold), `Retreat`, `FallBack`, `Follow`, `FollowEntity`, `NoOrder`, `NoFormation`. Follow stays none (a
  bodyguard should not dash off after his lord); hold / retreat / fall back as in step 33. No strong reason found to change.

**The 20% gate.** `SprintMinStartPercent` is the PLAYER's gate only now; the AI's gate is the keep plus the 0.2 s minimum
(`SprintStore.TryStartAi`). With keep 0 the old 20% gate would refuse a charge from 10% - a tired formation's last surge;
with keep 70 it would be meaningless. So a charge from 10% of a 5 s store sprints 0.5 s.

**Outcomes** (`SprintStart`): Started; `ReserveKept` (new) - keep > 0 and stored − keep x cap < 0.2 s; `BelowMinimum` - the
amount < 0.2 s otherwise (an empty store with no reserve, or a tiny use); `NoStore` - no capacity; `Off` - the switches or
use 0 (the logic does not even try: `AiUse` 0 returns before the mount check, as step 33's share 0 did).

**Code.** Core `Sprint.cs`: `SprintOrder` Charge / Advance / Move / None (step 33's `AdvanceOrMove` split); `SprintRules`
six ints (`AiChargeUsePercent` …) + `AiOrdersText` (in `Describe`); `SprintMath.AiUse / AiKeep / AiAmount / AiReserveStops`,
`AiMinSeconds`; `SprintStore.TryStartAi` (the player keeps `TryStart` with share 1); `SprintStats` - reserve kept per kind,
`KindText` (the summary). Module `AthleticsLogic.Sprint.cs`: `OrderChangedFor` → `TryStartAi`; ReserveKept counted (+ one
Info line per battle, then verbose `sprint-ai`); the first-order line names min(use, above the reserve); the first AI sprint
line: stored s of cap (%), the kind's use / keep, the asked T and what is left after it.

**Settings (live).** `SprintAiChargeUsePercent` 100, `SprintAiChargeKeepPercent` 0, `SprintAiAdvanceUsePercent` 50,
`SprintAiAdvanceKeepPercent` 0, `SprintAiMoveUsePercent` 20, `SprintAiMoveKeepPercent` 70 (0-100 each), group "Sprint";
read at each new order, so an MCM change applies at the next order.

**Old keys - config format 6.** `ConfigFile.Read` puts a key it does not know into `Unknown`, which `Write` carries on
every rewrite under "Not recognised - ignored" and the store logs as "not a setting of this version (typo?)" at every read
- for Anton's file that would be two stale keys forever. So `Migrate` (the established pattern, steps 13 / 14 / 20b / 22)
gets `RenameSprintShares`, run for a file of ANY format (the keys are only ever steps 33-35's; a hand-pasted one is renamed
the same way): `SprintAiChargeSharePercent` → `SprintAiChargeUsePercent` with its value (clamped; unless the new key is
already set; a non-number = dropped, the default used), `SprintAiMoveSharePercent` dropped (it was advance and move
together, as a share of what was stored - the new four take their defaults). Both leave `Unknown` and `Missing` is fixed
up, so ConfigStore rewrites the file once ("rewrote config.json as format 6 with the 2 migrated value(s)", each note
logged "migrated config.json at …: SprintAiChargeSharePercent 100 → SprintAiChargeUsePercent 100 (format 5 → 6; step
36: renamed …)"). FormatVersion 5 → 6 (the stamp; the step 13-22 rules are unchanged - their notes now say "→ 6").

**Logs.** Mission start / a change: "AI ON - on a NEW order he sprints min(use x his full store, stored - keep x it),
nothing below 0.2 s (no 20% start gate for the AI): charge use 100% / keep 0%, advance use 50% / keep 0%, move use 20% /
keep 70%; hold, retreat, fall back, follow none". First new order: "(move: its AI men on foot sprint min(20% of their full
store, what they hold above a 70% reserve))". First AI sprint: "… on a new move order - stored 5.00 s of 5.00 s (100%),
move use 20% / keep 70% → asked T 1.00 s (min(use x 5.00, stored - keep x 5.00); left after it 80%): x…". First reserve:
"first AI sprint held back by a reserve this battle: …". Summary: "sprint - AI: new orders seen - charge C, advance A,
move M, other O; sprints - charge (use 100% / keep 0%) N men (… s in all, avg … s), advance (…) …, move (…) …; not
sprinted: reserve kept N (charge 0, advance 0, move N), less than 0.2 s or empty: charge …, advance …, move …; …".

**Tests** 557 → 573 (SprintTests: Anton's examples for each kind as Theories, other orders none, keep above the store,
a move → a second move → reserve kept → the charge sprints the reserve, live settings + master switch + the sentence, the
summary line, the format-6 migration - rename, drop, already set, non-number, clamp, idempotent, the rewrite free of the
old keys; ConfigFile / Block tests: FormatVersion 6, "→ 6" in the notes). **Smoke** 81 → 82
(`SprintByOrderThroughTheLogic`: shock troops through the real logic - move 1 s, second move 0.5 s down to 70%, third
nothing (the line), the charge sprints 3.5 s, an advance 2.5 s, the move's keep live, the master switch, the summary;
step 33's smoke now expects an advance of min(2.5, what is left) and the new first-order / first-sprint text; step 35's
first-AI-sprint line too; `SprintDefaults` sets the six to DESIGN's values).

## Step 37 — the AI sprint: one RESERVE per order (DONE 2026-10-12)

**Anton (2026-10-12, same day as step 36):** simpler - ONE number per order, the reserve he keeps. "A man holding 90%
ordered to move sprints 10% until he drops to 80%." Purpose unchanged from step 36: men moved or advanced into position
still have sprint left for the real charge.

**The rule.** `SprintMath.AiAmount` = stored − keep x capacity (capacity = his FULL store now, as in step 36), 0 below
`AiMinSeconds` 0.2 s (plumbing) and for an order that never sprints (`SprintMath.AiSprints`: charge / advance / move;
hold, retreat, fall back, follow, no order = none - unchanged). Keeps (DESIGN initial = defaults): charge 0, advance 50,
move 80. Examples (5 s store): charge - all of it from anywhere (10% → 0.5 s, no 20% gate for the AI); advance - 100% →
2.5 s (50%), 90% → 40%, 60% → 10%, 45% → none (ReserveKept); move - 100% → 1 s (20%), 90% → 10%, 60% → none. A keep of
100 = that order never sprints (the old "use 0 = off"; now it counts as reserve kept, not as off - the logic no longer
returns early for it, so the summary shows those men under "reserve kept").

**What went.** `SprintAiChargeUsePercent`, `SprintAiAdvanceUsePercent`, `SprintAiMoveUsePercent` (schema, TraxSettings,
SprintRules' constructor - now 17 args: ..., refillExhaustedFactor, chargeKeep, advanceKeep, moveKeep, minSeconds,
topSpeedReach -, DESIGN rows, defaults.json; 128 → 125 settings), `SprintMath.AiUse` (→ `AiSprints`).
`SprintStart.ReserveKept` / `BelowMinimum` / `Off` keep their meanings: ReserveKept when keep > 0 and less than 0.2 s
above it; BelowMinimum = keep 0 and an (almost) empty store; Off = the switches or a non-sprinting order.

**Texts.** Mission start / a change: "AI ON - on a NEW order he sprints what he holds above the order's reserve (stored -
keep x his full store), nothing below 0.2 s (no 20% start gate for the AI): charge keeps 0%, advance keeps 50%, move keeps
80%; hold, retreat, fall back, follow none". First new order: "(move: its AI men on foot sprint what they hold above a 80%
reserve)". First AI sprint: "... move keeps 80% → asked T 1.00 s (stored - keep x 5.00; left after it 80%) ...". First
reserve: "... stored 4.00 s of 5.00 s (80%), move keeps 80% - nothing above the reserve, no sprint (it stays for the next
order)". Summary: "sprints - charge (keeps 0%) N men (...), advance (keeps 50%) ..., move (keeps 80%) ...; not sprinted:
reserve kept N (charge c, advance a, move m), less than 0.2 s or empty: ...".

**Config format 7** (`ConfigFile.FormatVersion` 6 → 7; the older steps' notes now read "→ 7" by themselves - the tag is
built from FormatVersion). `Migrate`: (1) a FORMAT-6 file only (the one format whose files carry step 36's keeps):
`MoveOldDefault` SprintAiAdvanceKeepPercent 0 → the default (50) and SprintAiMoveKeepPercent 70 → the default (80),
each logged "SprintAiMoveKeepPercent: 70 → 80 (format 6 → 7, the old default; step 37, ...)" - any other value is the
player's and stays (a format-7 file's 0 / 70 too); (2) ANY format: `DropRetiredSprintKeys` (replaces step 36's
`RenameSprintShares`) takes the three `SprintAi*UsePercent` AND step 33's `SprintAiChargeSharePercent` /
`SprintAiMoveSharePercent` out of `Unknown`, one note each ("SprintAiMoveUsePercent 20 dropped (format 6 → 7; step 37,
Anton: one number per order - ...)"), so the rewrite does not carry them under "Not recognised". Step 33's charge share is
no longer renamed (its target is gone: a charge now sprints all above its keep, which is what share 100 meant).
ConfigStore rewrites the file once as format 7 (its existing "rewrote config.json as format 7 with the N migrated
value(s)").

**Tests** 573 → 575 (SprintTests rewritten for step 37: Theories for each kind on Anton's numbers, other orders / keep
100 / keep above the store, move → second move = reserve kept → the charge sprints the 80%, an advance from full 2.5 s,
an empty store with keep 0 = BelowMinimum and with keep 50 = ReserveKept, settings live (keep 100 = none) + master switch
+ the sentence, the summary line, the format-6 → 7 migration (defaults moved, user values kept, use keys dropped, the typo
kept, idempotent, a format-7 file untouched) and step 33's shares dropped; Block / ConfigFile tests "→ 7"). **Smoke** 82
steps (`SprintByOrderThroughTheLogic` rewritten: move from full 1 s down to 80%, a second move nothing (the line), the
charge sprints the 4 s (80%) left, an advance from full 2.5 s, the move's reserve live at 40 (from ~50% a 0.5 s move),
the master switch, the summary; step 33's smoke: an advance after a short charge sprints down to the 50% reserve;
`SprintDefaults` sets the keeps 0 / 50 / 80).

## Step 41 — the shield wall holds its slots (DONE 2026-10-17; the research below was written before coding)

**Anton (2026-10-17, a playtest, a small fresh battle, nobody tired):** he ordered a formation into SHIELD WALL and let it
wait for the enemy. Men step 2-3 m forward out of the wall to swing at an enemy, then walk back to their slot; every man does
it, so the wall bulges and breaks. He wants them to keep their slot, "the way Realistic Battle Mod does it". DESIGN §2 "The
shield wall holds its slots", RESEARCH §O (the whole timing finding, with file + line).

**Verified in the v1.4.8 source (`%TEMP%\trax_decomp\`; RESEARCH §O)**
- Vanilla's `HumanAIComponent.ParallelUpdateFormationMovement()` writes `Agent.SetFormationIntegrityData(slot, facing,
  integrity…, keep)` with keep = catch-up && a 5th-slot item && a ≥ 50% ranged formation: for an infantry shield wall ALWAYS
  false - the native AI walks each man at the enemy he targets. The setter is public and native; no getter.
- **Timing - the write CAN win.** Vanilla runs it (a) on each AI man's own private `_cachedAndFormationValuesUpdateTimer`
  (0.45-0.55 s, staggered, made at spawn) from `Agent.TickParallel` inside the agent job, which `Mission.OnTick` starts AFTER
  every behaviour's `OnMissionTick`; and (b) at once on every order through an order controller (`AfterSetOrder` →
  `ForceUpdateCachedAndFormationValues`) and a few formation events. The next frame's `Mission.OnPreTick` waits for the job, then
  calls `OnPreMissionTick` - our write point. The timer's public `StartTime` moves exactly on the frames vanilla's update ran
  (the smoke proves it on the real Timer class), so we rewrite each held man in the very next pre-tick: vanilla's FALSE lives
  ≤ 1 frame per ~0.5 s per man. The native read is invisible (inside the engine) - but no read can fall more than one frame
  after vanilla's write without seeing ours.
- RBM (public source, read only for the idea - none of its code is here): a Harmony POSTFIX on that method - for AI men under
  a MOVE, not a siege attacker, not a column, it sets `ShouldCatchUpWithFormation` true (reflection on the private setter) and
  writes the same call with keep TRUE and the slot `GetCurrentGlobalPositionOfUnit(agent, false)`, except a man far from his
  slot (≥ 6 × the speed) AND behind the line; it also disables the formation frame on charges. It leaves the behaviour weights
  alone. We do the same idea WITHOUT Harmony: from our own MissionLogic, right after vanilla's write.

**Claude's calls - Anton can overturn any**
1. **On by default** - it is the fix for what he reported; nothing outside the three defensive arrangements changes; off is one
   switch, live. Group "Battle pace (AI)": the same aim as that group (a more defensive, more tactical AI infantry) and next
   to the shield infantry's own setting; a group of its own for one switch would be noise.
2. **Who**: AI men (never `IsPlayer` / a man the player drives - `IsAIControlled` false), active, on foot, in a formation, not
   `IsDetachedFromFormation`, formation arrangement shield wall / square / circle (`ArrangementOrder.GetArrangementOrderDefensiveness`
   == 1 - the smoke checks our set equals the game's), the order NOT charge / charge a target / the AI's tactical charge /
   attack a gate / retreat / no order; Stop, Move, Advance, the AI's advance, FallBack, Follow hold. Not a man fleeing
   (`IsRetreating`, native - read only past the cheap gates). Both sides alike.
3. **Where**: field battles, custom battles, hideouts, sieges for the DEFENDERS (a held line on the battlements is what a
   defender wants; RBM excludes only the attackers); NOT a siege's attackers (RBM's lesson: their climb / gate orders are Moves -
   held, they stood on the wall top); NOT naval battles / raids (ship decks, boarding - unknown ground; the step back skips naval
   for the same reason). Tournaments and arenas have no formations (the "no formation" gate).
4. **The slot = `blendWithOrderDirection: true`** - the very point vanilla hands the engine in this call, so the ONLY thing we
   change is the flag (RBM's `false` = the formation's estimated facing without the interval adjustment; for a standing wall the
   two are nearly the same). The "ahead of his slot" measure uses the same point and `CurrentDirection`.
5. **A scattered formation is held too** (vanilla, deviation > 3 × speed, writes zeros + false and lets everyone go): forming
   the wall is the order's point; RBM does the same.
6. **The escape** (RBM's, on vanilla's catch-up radius `WallMath.CatchUpSpeeds` 6): far (≥ 6 × the formation's average top
   speed, ~20 m) AND behind the line (behind `CachedAveragePosition` along `CurrentDirection`) = left to vanilla (he runs to
   rejoin); far AHEAD = held (walked back). No speed cached yet = never far.
7. **No reflection on `ShouldCatchUpWithFormation`**: it gates only the speed cap; vanilla keeps setting it (true near the slot,
   false far) - a held man far ahead runs back uncapped, as he should. **One read-only reflection**: vanilla's update timer, once
   per man (`GameWallBody.TimerField`, a FieldInfo looked up once; a missing field → the fallback, 0.1 s rewrites, one `[wall]`
   line, the summary counts the men). The house's other reflection precedent: the tournament AI-level fix (`_AILevelMultiplier`).
8. **Live**: off (ShieldWallHoldsSlots or the master switch) writes VANILLA's own value (its formula, `GameWallBody.Release`)
   for every held man at once - "simply stop writing" is not enough: under a Stop order vanilla never writes the flag, so ours
   would stay forever. A man let go by a gate (charge, retreat, mounted, detached, escape, the player took him…) gets the same
   release. Leaving the field: no engine call. Back on: held again at his next look (≤ 0.25 s).
9. **Plumbing constants (Core `WallMath`, documented, not settings)**: `PollSeconds` 0.25 (the look; brace's precedent),
   `SampleSeconds` 0.5 (every second look), `FallbackRewriteSeconds` 0.1, `ContactMetres` 5 and `AheadNearMetres` 1 /
   `AheadFarMetres` 2 (the log only), `CatchUpSpeeds` 6 (the game's own number).
10. **Interactions**: step 16's backpedal and step 23's brace / the AI timer act on the AI's INPUT (`OnAIInputSet`), a different
    channel - all keep working on a held man (his attacks paused, guard up, a backpedal; the native AI then pulls him back to his
    slot). The sprint's speed factor is untouched. RTS Camera / Command System also hook the formation movement path on the worker
    job; we write only on the main thread between jobs, so vanilla's / another mod's value can stand ≤ 1 frame before ours. RBM
    writes the same flag (declared incompatible already).

**Built** - file map in CLAUDE.md "Layout". Core `ShieldWall.cs` (WallArrangement, WallVerdict in gate order, WallFacts, WallRules,
WallMath), `ShieldWallStats.cs` (WallAheadSamples, WallWriteKind, WallStats + the 5 [summary] lines). Settings 125 → 126:
`ShieldWallHoldsSlots` (true, Live) at the end of "Battle pace (AI)". No config migration (a new key takes its default). Module
`WallBody.cs` (WallState, IWallBody, GameWallBody, WallBodyDefault), `AthleticsLogic.Wall.cs` (StartWall, TickWall from
`OnPreMissionTick`, LookWall, the held list with swap-remove, ReleaseAllWalls, WallLeftField from Forget, WallFailed, CloseWalls,
WriteWallSummary); TrackedAgent.Wall. Logs: `[wall]` mission start, switch OFF / ON lines, the first man held in full, the no-timer
line, verbose let-go lines (bucket `wall`); the [summary] "wall" lines. Tests 575 → 610 (`ShieldWallTests`). Smoke 82 → 86 steps
(`Program.Wall.cs`: the stand-in body, the game's timer moving exactly with vanilla's update + the arrangement map, the real logic
end to end, the cost - 1000 men held, 0.016 ms a pre-tick, 0 bytes, ~6 writes a man a second; the master-switch step lets the
held men go). Deploy: build, AssemblyGuard, smoke green; the install copy failed - the game was running (DLL locked).

**UNVERIFIED — only the game can tell (PLAYTEST D11, L6h)**
1. The native AI honours the flag for infantry (it was only ever set for shield men of ranged formations) → `wall slots ON`
   "more than 1 m ahead" (in contact above all) well below `wall slots OFF`'s. If ON ≈ OFF: the flag alone is not enough - the
   next lever is the formation frame / the behaviour weights (RBM changes neither), tell Claude.
2. Held men still FIGHT what reaches them (Anton's eye) - not a wall of statues. If they stop swinging at men 1.5 m away, the
   native AI's "keep" may be too strict for the front rank.
3. `vanilla's own updates seen … one every 0.5 s per man` (the timer read works in game) and `men whose update timer could not
   be read 0`.
4. Cost → the summary's `cost avg … ms a tick` in a 400+ man battle.
5. Square and circle look right (they were not in Anton's report), and a siege defenders' wall line on the battlements.

## Step 42 — a small step out of the wall: the leash (DONE 2026-10-17)

**Anton (2026-10-17, his playtest of step 41, log 19:26):** the hold WORKS - in contact 0.6% of men > 1 m ahead of their slot ON
(mean 0.09 m) vs 17.8% OFF (mean 0.70 m, the farthest 9.6 m). But his eyes, ON: they "looked many times like they were about to
reach to hit but stopped, maybe as the enemy took a few steps back, so they very rarely hit"; OFF they ran far out of the square
chasing. His wish: "give them just a bit of a step if the enemy moves a small step back, but not as far".

**The mechanism - a two-state leash per held man (Core `LeashMath`, `ShieldWallLeash.cs`)**
- **FREE** = vanilla's own value given back (`GameWallBody.Release` - vanilla's formula, keep false for infantry), so the native AI
  closes on its enemy and swings, exactly as without step 41 - while he stands **less than L out of his slot AND an enemy is close
  in front of the slot**. He stays in the held list; nothing is written while he is out (vanilla's ~0.5 s update writes false
  anyway - that IS the step; the after-vanilla rewrite skips him).
- **HELD** = step 41's TRUE, written at once, once he is **L or more out**, or **no enemy is close** - he walks back (the flag pulls
  him to his slot, as step 41's numbers show).
- **Hysteresis, both edges**: after a step he may step again only once **back in his slot** - out ≤ `RearmAt(L)` = min(0.3 m, L/2)
  (`BackInSlotMetres` 0.3: step 41's held men stood 0.09 m out on average in contact; half the leash keeps a gap for short leashes);
  and a man out stays free until his enemy is 0.5 m farther than the distance that freed him (`EnemyHysteresisMetres`). Without the
  first, a man whose enemy keeps backing off would sit at L stepping in 0.1 s jerks - a bulge of L. With it: step, return, step.
- **"Close in front"** = the enemy is in front of his slot's line (out > 0) and within **L + `StrikeReachMetres` (2.0 m) of the
  SLOT**. Why from the slot and why 2 m: a one-hander's strike lands about 2 m centre to centre (arm ~0.7 + blade ~0.9 + the target's
  body ~0.35); measured from the slot, a step of at most L brings him into reach - an enemy farther off would only draw him to the
  leash and back without a blow (Anton's "reached and stopped" again, just 1 m further out). Step 41's `ContactMetres` 5 is the log's
  "in contact", far too wide for this; a per-weapon reach (`GetRealWeaponLength` + the arm) would cost more native reads per look for
  a few tenths of a metre - the plain constant is the call.
- **The enemy**: `Agent.GetTargetAgent()` (the native AI's own target - the very man vanilla would walk him at), else
  `Agent.ImmediateEnemy`; only an active enemy (`IsEnemyOf`). Native reads - asked only when he could step now or is out
  (`LeashMath.NeedsEnemy`), never at L 0.
- **"Out"**: along the formation's facing for a shield wall (step 41's "ahead"); for a **square or circle** along the direction from
  the formation's centre (`CachedAveragePosition`) to his slot - their sides and backs face OUTWARDS; along the facing a side man's
  enemy is never "in front" and a back man could never step (the facing is used when his slot is within 0.5 m of the centre). Step
  41's "ahead" samples stay along the facing (comparable with its ON / OFF lines).
- **L = 0** = step 41's hard hold exactly: `LookWall` takes the leash branch only while L > 0 or a man is still out - at 0 the same
  calls in the same order (the smoke runs 3 s with and without an enemy in front: identical writes, 0 enemy reads; step 41's own smoke
  now runs pinned at 0).

**Timing (the brief's point)**
- A man OUT is looked at every **0.1 s** (`LeashMath.LookSeconds`) from the per-frame loop: he walks 1-3 m/s, so the leash bites
  within 0.1-0.3 m of L (a 0.25 s look would let him 0.75 m past). Measured offline at 2 m/s: reined at 1.03 m with L 1. They are few.
- HELD men keep step 41's cadence (right after vanilla's update, and the staggered 0.25 s look) - and every one of those looks also
  runs the leash, so a held man in his slot is freed within ≤ 0.25 s of an enemy coming close (often at once: the after-vanilla look).
- A man first seen in his slot may step on that very look.
- Reined: the write follows in the same look; the engine's reaction to the flag is invisible (as step 41) - one frame at worst.

**The alternative (not built - the brief said do not build both)**: hand the engine a slot point moved up to L toward the target as
the integrity POSITION (keep TRUE always). The decompile shows only that `Agent.SetFormationIntegrityData` passes the position
straight to native (`MBAPI.IMBAgent.SetFormationIntegrityData`) - vanilla writes Vec2.Zero there for a man beyond the catch-up
radius, nothing managed reads it back, and nothing shows how the engine's movement uses it (a target point? a pull centre with
`deviationOfPositions` as a radius?). So the on/off leash it is - it uses only behaviour we have SEEN (true = holds, false = walks at
the enemy). Fallback idea if the on/off steps look jerky in game: the moved point, tried in a test battle first (RESEARCH §O item 8).

**Plumbing constants (Core `LeashMath`, documented, not settings)**: `StrikeReachMetres` 2.0, `EnemyHysteresisMetres` 0.5,
`BackInSlotMetres` 0.3, `LookSeconds` 0.1, `OverMetres` 0.5 (the log's overshoot mark), `MinRadialMetres` 0.5, `MaxMetres` 3 (the
setting's range).

**Built** - Core `ShieldWallLeash.cs` (WallLeash, LeashMove, LeashEnd, LeashMath, WallLeashStats + the [summary] "wall step out" line),
`WallRules.StepOutMetres` / `Leash` (0 off the switches; clamped 0-3; the 2-argument constructor = 0), `WallWriteKind.Leash`;
`ShieldWallStepOutMetres` (1.0 m, Live, right after ShieldWallHoldsSlots; 126 → 127 settings; no config migration - a new key takes
its default: `ConfigFile` counts it Missing → default). Module: `IWallBody.TryEnemy` (+ GameWallBody), `WallState.Leash` /
`NextLeashLook` / `EverStepped`, `AthleticsLogic.Wall` - the leash in `LookWall` (goto Sample: a man out writes nothing), `Manage`
(the held list - a man first seen stepping out resolves his timer first), `NoteLeash`, `EndStep`; every let-go path ends a step
(LookWall's gates, ReleaseAllWalls - the switch / master switch -, WallLeftField, WallFailed, CloseWalls = still out). Logs: the
leash in the mission-start sentence ("a held man may step up to 1.0 m …" / "no step out (… 0 - the hard hold of step 41)"), a live
change ("ShieldWallStepOutMetres 1.0 → 2.0 m mid-mission at … s: …"), the first step out and its end in full, verbose steps
(bucket `wall-step`), writes "by the step-out leash", the [summary] line. Tests 610 → 625 (`ShieldWallLeashTests`). Smoke 86 → 89
(`Program.Leash.cs`).

**Gotcha - Smart App Control (this PC, 2026-10-17)**: Windows' reputation check is ENFORCED here
(`HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy` VerifiedAndReputablePolicyState 1). It blocked the freshly rebuilt, unsigned
`TraxCombatEnhancements.dll` in `tools\OfflineSmoke\bin\Release\net472` ("An Application Control policy has blocked this file",
0x800711C7; CodeIntegrity events 3033 / 3077) right after the same source had passed the smoke - the verdict is per file hash, so
a rebuild (a new commit stamps a new hash) can pass or fail. Not our code. Never change that setting; if it blocks, say so and hand
Anton the deploy line.

**UNVERIFIED — only the game can tell (PLAYTEST D12, L6h)**
1. A freed man really closes and LANDS blows on an enemy who stepped back (Anton's eye) - the point of the step.
2. Nobody chases far: `wall step out` "in contact past the leash + 0.5 m" near 0, and `wall slots ON` "more than 2 m" stays near
   step 41's 0%; "reined at the leash … m past it" ~0.1-0.3 m.
3. The step rate: "N steps out by M men (x a man a minute while held)" - a few a minute for front men is the picture; dozens = the
   enemy flickers on the edge (raise the hysteresis), ~0 with enemies in contact = the target read sees nobody in front (tell Claude).
4. Squares / circles: side and back men step OUTWARDS, not along the formation's facing.
5. The native AI may take a moment to start walking after a free - if steps look late, the after-vanilla look is the lever.

## Step 43 — aggression follows Athletics, the fever of a charge, cheaper swings (DONE 2026-10-17)

**Anton's idea (2026-10-17)**: people are defensive by nature - they don't throw their lives away and won't swing until they
tire; but when the general says NOW!, everybody surges because each knows his comrades do; the more experienced follow it
longer, the tired shorter; the surge fades. Today AI men ran into each other swinging until exhausted - our tiredness was the
only brake. Tournaments (no orders) calmer. Big values first "to see the effect well". Three changes from Anton while it was
being built: (1) the fever sprint FADES with the fever - a roll every `FeverSprintCheckSeconds` at `FeverSprintChancePercent` ×
left ÷ full instead of "once per fever"; (2) `CostPerWrongSideShieldBlock` 5 → 2 in the same migration as `CostPerBlow` 10 → 3;
(3) THE REDESIGN - the first draft's flat calm factor (`AiCalmFactor` 3, a peak `FeverAggressionMultiplier` 3 on the calm
values) was replaced, before any release, by aggression that follows the bar ("a man at work doesn't hold a pace until he
drops; the more tired he is, the less he swings") and an ABSOLUTE fever peak. No switch for the old way (Anton).

**The lever (RESEARCH §P)**: the base model's `SetAiRelatedProperties` assigns `AIAttackOnDecideChance` (0.1 × 0.48 × (3 − D),
clamped 0.05-1) and `AIAttackOnParryChance` (0.08 − 0.02 D) with `=` on every recompute; D = 0 charge, 1 other orders, 2 a
non-charging shield wall / square / circle (0 for a man in no formation). Neither reads the AI level. The stat decorator multiplies
both by the man's share S (`SpeedPenalty.ScaleAttackDecisions`, `FeverMath.ApplyChance` - × then clamp 0-1).

**The rule**
- Tired share T = E + (F − E) × bar, bar = points ÷ his FULL pool (`Fighter.Fraction` - the brace's reading, not f, so it slides
  over the whole bar); F = `AiAggressionFreshPercent` 60, E = `AiAggressionExhaustedPercent` 20. A wound's cap is in the bar
  already - no special case: a man capped at half health is at most at 0.4.
- The fever: P = `FeverAggressionPercent` 80, ABSOLUTE. Armed: S = max(T, P). Burning: S = T + (P − T) × left ÷ full, never below
  T (T keeps sinking as he fights - the fade lands on his CURRENT tired share). P ≤ T → the fever adds nothing.
- Applied: S rounded to 0.05 steps (`FeverMath.Quantize`, `ShareStep`); a step = a recompute. 100 / 100 = the game's AI.
- Values on a charge (0.144): fresh 0.086, half 0.058, empty 0.029, fever peak 0.115; riposte 0.08 → 0.048 / 0.032 / 0.016 / 0.064.

**Built** (file map in CLAUDE.md "Layout")
- Core `Fever.cs`: `FeverRules` (live; ModEnabled FIRST, AthleticsEnabled - AggressionOn; Enabled = + FeverEnabled; SprintOn;
  Describe with the charge's numbers), `FeverMath` (TiredShare, AggressionShare, Quantize, DecisionFactor, ApplyChance, Store,
  Share, BurnRate, Group, SprintChance, SprintRoll, share bands; plumbing ShareStep 0.05, PollSeconds 0.25, MaxPercent 200),
  `FeverStore` (Arm → Armed / Rearmed / KeptLarger / Repeat / NoStore / Off / NotAggressive, StartBurn, Burn with the shield live,
  SprintDue, Stop), `FeverPhase`, `FeverEnd`, `FeverArm`, `FeverSprintRefusal`. `FeverStats.cs` + 10 [summary] lines.
- 12 settings (139), a new group "Aggression and fever (AI)" after "Sprint": `AiAggressionFreshPercent` 60,
  `AiAggressionExhaustedPercent` 20, `FeverEnabled`, `FeverPointsPerSecond` 10, `FeverAggressionPercent` 80, `FeverContactMetres` 5,
  `FeverShieldDurationMultiplier` 0.7, `FeverSprintToClose`, `FeverSprintMinMetres` 3, `FeverSprintMaxMetres` 12,
  `FeverSprintCheckSeconds` 1, `FeverSprintChancePercent` 50. All Live. The percents 0-200.
- Config format 8: `CostPerBlow` 10 → 3 and `CostPerWrongSideShieldBlock` 5 → 2 in ANY older file still holding the old value (both
  defaults go back to steps 5c / 22, so every older format knew them - the brief said "format 7"; older follows the same rule); a
  tuned value kept; logged "migrated config.json …".
- Module: `TrackedAgent.Fever / DecisionFactor / FeverContact / FeverLastLook / NoFormation`; `SpeedFactorsFor` hands the decorator
  `decision`; the decorator writes it for `!IsMainAgent && MountAgent == null` (the game's values captured BEFORE step 5e's m for the
  summary); `RetargetSpeed` carries `RetargetDecision` (every charge, refill step, wound, settings change, the master switch and
  Athletics off re-target it); `AthleticsLogic.Fever.cs` - TickFever (staggered like the brace: each man ~every 0.25 s), PollFever
  (player / rider → his fever ended; contact = `EnemyWithin(FeverContactMetres)`; the burn with `IWeaponFacts`' shield read; the
  sprint check; RetargetDecision), FeverOrderFor (from the order poll), FeverSprintCheck, EndFever on every path (faded, order,
  switched off, player control, mounted, left the field, mission end, error), NoteFeverAttack (StartRelease), NoteDecisionValues
  (decorator), the [fever] lines, WriteFeverSummary. The order poll (`PollOrdersFor`) now runs while the AI sprint OR the fever is
  on and hands each changed formation's men to both; `SprintFormationRead.ByPlayer` = `!Formation.IsAIControlled`.
- Tests 625 → 642 (FeverTests: 17). Smoke 89 → 91 steps (FeverThroughTheLogic, FeverCostStaysFlat) + the lever check + the
  master-switch step (a fever armed, the mod off: the game's values, the fever ended, factor 1; back on: 0.6). The older steps pin
  the share at 100 / 100 and the fever off (`FeverOff` in AthleticsDefaults).

**Decisions (Claude's calls - DESIGN interpretation 36)**
- Melee only: the two decision chances. Archers keep step 21's wait; `AiShootFreq` / the aim untouched.
- AI on foot only. RIDERS keep the game's values: a rider's swing is timed by his pass - a less eager rider rides through without
  striking (cavalry crippled, not more careful); step 21 left riders out for the same reason. Never the player. A man who mounts
  loses his fever (counted "mounted").
- No 0.05 floor of ours: the game's clamp belongs to its own formula; we write after it (5e's rule). A tired shield wall decides at
  0.01 - very patient; it still blocks, and the brace / wall hold are unchanged.
- With 5e's `AttackRateAiDecisions` on (off by default) the factors multiply: the game's × m × S.
- Both need AthleticsEnabled (the share and the store ARE his Athletics; Athletics off = the game's AI, like every Athletics feature).
- Re-arm: the same order GROUP again leaves a running fever; the other aggressive group re-arms to max(left, fresh) - a fresh one
  larger = back to the peak (burning stays burning). A non-aggressive order ends it, armed OR burning. "Armed" counts fevers.
- The fever sprint: a roll every 1 s while armed or burning, chance 50% × left ÷ full (full while armed); the cheap gates first
  (sprint switches, sprinting, store, the reserve - `SprintMath.AiAmount` > 0, so the 0.2 s minimum applies), then the two proximity
  reads (3 / 12 m), then the dice (`FeverDice`); a start is `SprintStore.TryStartAi` + BeginSprint (counted in the sprint's AI starts
  too). Several per fever, rarer as it fades; the reserve caps the total.
- Heroes: the same rules - their big pools give them long fevers (a 300-skill lord: 30 s). Not built differently.
- In contact = an enemy within FeverContactMetres at his last look (the burn start and the summary's proof alike).

**A tired man now has TWO brakes** (Anton asked to note it): he DECIDES to attack less often (the share sliding to 20%) AND each
attack is followed by the pause by tiredness (step 13), step 21's battle-pace share and the swing stack's waits; the brace floors still
stop him altogether. None of those was changed - Anton playtests the balance first. The risk to watch: two brakes may slow a tired
fight too much (deadlocks between two spent lines; a tournament final between two exhausted men). The levers, in order: raise
`AiAggressionExhaustedPercent`, or lower `ExhaustedAttackSpeedPercent`'s pause.

**Recomputes**: the share moves 0.05 per 12.5% of the bar (8 steps over a full bar at 60 / 20), so a fighting man asks a recompute
each ~1/8 of his bar spent or refilled: at 3 points a swing, a 100-point man in a busy fight (~30-60 points a minute out and in)
~2-5 a man-minute; a fever adds ~5 (the arming + 4 steps down from 0.8 to 0.6 for a fresh man; the smoke measures exactly 5). The
summary's "fever recomputes" line measures the real rate per AI man-minute - inside the 50-a-tick budget the speeds and the sprint
(~12 a charge) already share.

**Interactions**
- Brace (step 23): a bracing man's attack bits are out of his input whatever his decision values - the fever never overrides a brace.
- Battle pace (step 21) and the swing stack (step 32) time what happens AFTER an attack; the share changes how often he DECIDES to
  start one. They stack (see "two brakes").
- The wall hold (steps 41/42): a shield wall on a charge is not held anyway.
- Step 37's reserve caps the fever sprint; a charge order's own sprint (keeps 0) usually spends the store far away, so the fever
  sprint matters most on an advance (keeps 50) and later in a long fever after the store refilled.

**Measured ([fever] tag)**: the mission-start settings line (with the charge's values fresh / half / empty / peak); first decision
values written, first fever armed, first burning, first fever sprint, first faded - in full; settings changes; switched off.
[summary]: settings; armed by order kind and by you / the AI general, re-armed / kept / repeats, not armed (horse, no Athletics,
player); contact - burns of armed, arm → contact mean / max, never met one by end reason; burn length shield vs no shield against the
store, switched mid-burn, all ends; recomputes per AI man-minute and per fever; fever sprints by the fever left (100-67 / 67-33 /
33-0 %) and refusals by reason; THE PROOF - AI melee attacks (releases on foot) per man-minute in contact by bar band 100-75 /
75-50 / 50-25 / 25-0 %, in a formation without a fever and in a fever, and on its own line in NO formation (tournaments, arenas,
duels); the values written (the game's → ours) by bar band and in a fever; the look's cost.

**Cost**: 1000 men in a fever, half burning, half rolling for a sprint: 0.005 ms a tick, 0 bytes (smoke).

**Gotchas**: `sed -i` in Git Bash rewrote two CRLF test files as LF (no content change) - caught by `git ls-files --eol` and
restored with `git checkout`; every later edit went through Python with `newline=''`, keeping each file's own line endings. Smart
App Control blocked the smoke's freshly built DLL once (step 42's gotcha, per file hash) - a commit and rebuild passed.

**UNVERIFIED - only the game can tell (PLAYTEST D13)**
1. The native AI uses the two values per decision so a share of 0.6 / 0.2 means that many fewer attacks in contact → `fever proof`
   by bar band (the rate falling from the 100-75% band to 25-0%) and a battle at 100 / 100 vs 60 / 20 (RESEARCH UNVERIFIED 25).
2. In a fever ~1.3x (fresh) to 2-4x (tired) the rate without, in the same band → the `in a fever` row vs the `no fever` row.
3. No deadlock: tired lines still trade blows (not two walls parrying forever) - Anton's eye, and the 25-0% band not at 0.
4. Contact at 5 m is a fair "the fighting started" → `armed → contact mean` a few seconds for a charge from 30-50 m.
5. The shield split → `burn length with a shield … (x0.70)` vs `without … (x1.00)`.
6. Fever sprints happen but are not spam → a few per hundred fevers on a charge, more on an advance.

## Step 44 — the fever mark under ALT (DONE 2026-10-17)

**Anton's ask (2026-10-17)**: when he holds ALT the game shows a small icon above each of his soldiers; he wants to see there which
soldiers are in FEVER (step 43).

**Vanilla's icon (RESEARCH §Q)**: `MissionAgentLabelView` puts a 3D metamesh (`troop_banner_selection`, the troop's banner) on each
human agent's visuals at build, keeps them in a PRIVATE `Dictionary<Agent, MetaMesh>`, shows them for YOUR side only (never the
enemy) while ALT is held / the "always show friendly troop banners" option is on / in deployment, else only for the SELECTED
formations while the orders flag shows; it fades them by the camera distance to the feet + 2 m (0 under 1.5 m, a line to 8 m) and
REWRITES the mesh's vector arguments on every highlight change and every tick near the camera. **So we never tint it** (Claude's call,
told to Anton): reflection into a private list, racing vanilla's writes - the two would fight. Ours is a separate 2D Gauntlet layer.

**The design (DESIGN §3 item 3b, interpretation 37)**
- **When**: step 20's rule - vanilla's own formation-marker flag (`MissionFormationMarkerVM.IsEnabled`: ALT held OR the orders menu
  open), read live through a second `GauntletFormationMarkers` reader; the copied rule (HudFrame.ShowIndicatorsKey / OrderMenuOpen)
  when unreadable. WHY this and not the icon's own rule: one rule for both of our ALT overlays (the labels and the marks come and go
  together), it is readable (the icon's state is private), and the orders menu - where you decide to charge - is exactly when you want
  to see who is still surging. FeverEnabled off = no marks (checked first in `ViewConditionMet`, with its own log line). The common
  HudGate rules apply (ModEnabled FIRST, AthleticsEnabled, ShowFeverMarks, Hide battle UI, photo mode, a fight); NeedsPlayer false.
- **Who**: AI men whose fever runs (armed or burning) - `AthleticsLogic.TryGetFever(agent, out armed, out share)` (public, like
  TryGetReading) and its allocation-free walk `FeverWalkCount` / `TryGetFeverAt(i, …)` over the dense array (0 while no fever runs; the
  three switches read directly, not FeverRules.From - a hot walk). Never the player (IsPlayer), never a calm man. Yours and allies'
  always; the enemy's behind `FeverMarksShowEnemy` (on - vanilla's icon never marks the enemy; seeing their surge is the point).
- **The look**: a slim BAR, 22 × 5 UI px (`FeverMarkWidth` / `FeverMarkHeight`, Advanced - step 20's precedent put its layout in
  settings), a dark 1-px frame (FillBarWidget inside 1-px margins), the fill = the fever left (1 armed). Colour `FeverMarkMath.Tint`:
  orange #FF8C1A armed and at the burn's start → yellow #FFE040 as it empties (a straight line in the share), alpha 1 → 0.45, 0 when
  empty. Why a bar: the length says HOW MUCH surge is left, the colour the state - one native sprite (BlankWhiteSquare_9), no rotation
  (Gauntlet widgets cannot rotate a square into a diamond), no new sprite to verify. The colours are UI constants (like the bars').
- **Where**: the feet (`Agent.Position`) + `FeverMarkHeightMetres` 2.5 (just over vanilla's 2 m icon), projected every frame with
  `MBWindowManager.WorldToScreen(CombatCamera, …)` (vanilla's formation markers' call) - the bar's bottom centre on that point.
  Vanilla's icon fade (`FeverMarkMath.Fade`: 0 under 1.5 m, a line to 8 m) × the fever's alpha, baked into both colours (no
  AlphaFactor question). Size × the UI scale × `SizeFactor` (1 to 20 m, a line to 0.6 at 60 m). The screen and the UI scale from our
  own layer's UIContext (vanilla's marker layer as a fallback).
- **Cost**: a grow-only POOL of marks per show (mark i = the i-th drawn this frame - not keyed by man: a bar placed anew every frame),
  hidden past the frame's count; `FeverMarkFrame` reused (arrays double only when a battle has more men in fever than ever before).
  Each eligible man is projected BEFORE the pick (an off-screen man never takes a slot from one on the screen - the first draft
  projected only the picked, and in a crowd the nearest men are often off the screen's sides), then the nearest `FeverMarkMax` (200)
  by camera distance (`Array.Sort(keys, items, 0, n)` - allocation-free; only when the cap bites).

**Built** (file map in CLAUDE.md "Layout")
- Core `FeverMarks.cs`: FeverMarkSkip (NotPlaced, EnemyOff, Behind, TooClose, OffScreen, Capped - in the order asked),
  FeverMarksHidden (ModOff FIRST, AthleticsOff, FeverOff, SwitchedOff), FeverMark, FeverMarkFrame, FeverMarkRules (live), FeverMarkMath
  (Tint, Fade, SizeFactor, PreSkip, Pick, Size, Place, OnScreen, the names), FeverMarkStats (+ the [summary] line).
- Module: `Hud/FeverMarkSource.cs` (IFeverMarkSource - TryReadShown, NewShow, BeginFrame, Locate, Project, Describe; GameFeverMarks),
  `Hud/FeverMarkView.cs`, `Hud/FeverMarksVM.cs` (FeverMarksVM + FeverMarkVM: IsShown, PositionX/Y, Width, Height - Scaled* pixels -,
  Fill, FillColor, FrameColor), `module/GUI/Prefabs/TraxFeverMarks.xml`; attached in `AthleticsLogic.AttachHud` after the ALT labels;
  `AthleticsLogic.Api.cs`: TryGetFever, FeverWalkCount, TryGetFeverAt.
- 6 settings (145): `ShowFeverMarks` (on), `FeverMarksShowEnemy` (on), `FeverMarkMax` 200 (1-1000) in the ALT group - renamed
  "Hold ALT: formation markers and fever marks" (was "Formation markers (hold ALT)"; group names are not stored anywhere but MCM's
  page and the files' headings); Advanced `FeverMarkWidth` 22, `FeverMarkHeight` 5, `FeverMarkHeightMetres` 2.5. All Live.
- Log: `[hud] attached: fever marks …`, the first build in full (later builds / removals by the condition verbose), `[hud] fever marks:
  first marks drawn at … (show #N) - N men in a fever (yours and allies, enemy; armed, burning), N marks drawn (the nearest N of M -
  FeverMarkMax), skipped: …; screen …, UI scale …; the layout; the nearest three; source …`, `FeverEnabled switched off …`, the
  [summary] `hud: fever marks - shown Nx, on screen T s over N frames; marks a frame: mean / max; most men in fever at once (by side);
  frames at the cap; men skipped by reason (summed over frames); cost µs a frame (most); errors`.
- Tests 642 → 655 (FeverMarkTests: 13). Smoke 91 → 95 steps (the prefab, the view over the REAL logic's fevers with a stand-in camera,
  the fail safe, the cost) + the master-switch step (the mark goes with ModEnabled, TryGetFever answers nothing meanwhile).

**Measured (smoke)**: 1000 men, 400 in a fever (half the enemy's, 60 behind the camera, 78 off the screen), the cap 200: **~0.03 ms a
frame, 0 bytes** over 300 frames with every mark moving. In game add the engine reads per man in a fever (Agent.Position, IsActive,
one WorldToScreen - a few hundred native calls, ~tens of µs) and Gauntlet's own layout of up to 200 small widgets (only while ALT
is held). The summary's `cost` is the view's own stopwatch around all of it but Gauntlet's drawing.

**Gotchas**: the smoke's `[error]` count - `AthleticsLogic.Failed` reports a site once per mission and the earlier fail-safe steps
spent "hud.tick", so the fever fail safe proves its error by the view's own count and the DISABLED line. A smoke step that calls
`AthleticsDefaults()` must end with `ConfigStore.Reload(...)` (DamageDefaults inside it pins DamageRandomPercent / the reduction, and
the MCM steps at the end need the file's values back). Smart App Control blocked the smoke's freshly built Core DLL once again (per
hash, step 42's gotcha); a one-line doc edit (a new hash) passed.

**UNVERIFIED — only the game can tell (PLAYTEST D14)**
1. The bar sits above the man's head, just over vanilla's icon, and rides him without lag → Anton's eye; `FeverMarkHeightMetres` live.
   (Our view ticks after the camera's update like the ALT labels - same-frame reasoning as step 20; if a running man's bar trails,
   tell Claude.)
2. The screen and UI scale from our own layer's UIContext → the first-marks line's `screen W x H px, UI scale S`.
3. The FillBarWidget inside 1-px margins draws the fill at 5 px tall (3 px inside); the colours fade (orange → yellow) → eye.
4. 200 marks cost no visible frame time → the summary's `cost` and Anton's frame rate in a 500+ battle.
5. The enemy's marks show on their charges → the first-marks line's `enemy N`.

## Step 45 — research + plan: the IMPACT system (DONE 2026-10-17, no code)

Anton (2026-10-17, while playtesting 41-44): kicks / bashes, blocks (right and wrong side) and clean hits can stagger, drop a
shield or knock down, by both men's CURRENT Athletics, the weapon, the swing and the skill; longer staggers and slower get-ups
when exhausted; the player too; "put it in order for me, I may be missing something". Twelve points, all kept.

**Written**: `docs/IMPACT.md` (the whole picture: the 12 points mapped, one model, worked examples, ~60 named parameters, the
build order I1-I7, 14 open questions with recommendations, what a combat designer would also ask) and RESEARCH §R (every lever,
file + line) + UNVERIFIED rows 27-33. Nothing in DESIGN / defaults.json / TASKS_TODO's BUILD ORDER yet - Anton approves first.

**Verified in the v1.4.8 decompile (%TEMP%\trax_decomp, RESEARCH §R)** - the facts the design rests on:
1. Every melee collision builds its blow and asks the model's `Decide*` (ShrugOff, Dismount / KnockBack + KnockDown, Rear) -
   Mission.cs 5496-5590 - but a BLOCKED / parried blow is never registered (5368): its answers are thrown away. So knocking down a
   blocker needs a blow of our own (`Agent.RegisterBlow` is public; vanilla builds its drowning blow that way, Mission.cs 3668-3683)
   or a crush-through.
2. A registered blow with `InflictedDamage <= 0` plays no reaction (Agent.cs 5438); behaviours' `OnAgentHit` / `OnScoreHit` run
   BEFORE the engine's `HandleBlowAux` (5468 / 5475) with `in Blow` - no callback can change a landed blow; only the decorator can.
3. Vanilla: every landed kick / bash knocks BACK (MCMH 60-63), a kick never knocks DOWN (null weapon, MCMH 88).
4. `CalculateDefendedBlowStunMultipliers` (ref attacker / defender stun on every block, MCMH 307) - capped at `StunPeriodMax` 0.8
   after it (308-310; `ManagedParameters.SetParameter` is public) - and it runs inside `GetDefendCollisionResults`, which is
   `[MBCallback(null, true)]` = MULTI-THREAD callable (Mission.cs 6455): read-only state, thread-safe dice, Interlocked counters.
5. `act_stagger_{forward|backward|right|left}[_2|_3]` exist (ActionIndexCache.cs 159-181; types StrikeLight / Medium / Heavy),
   unused by vanilla's managed code; RBM plays them with `SetActionChannel(0, …, actionSpeed)` and crashed a HORSE with
   `act_stagger_backward_3` (not in the horse's action set). Knockdown = one fall-and-rise action of type Fall (23)
   (`act_strike_fall_*_back_rise`, picked by monster_usage_sets.xml) - "get up slower" = slow that action.
6. `SetCurrentActionSpeed` = an absolute speed for the current action; vanilla only uses it on actions it started (step 5e). The
   reaction appears the tick AFTER the hit callbacks - our existing channel polls see its rising edge for free.
7. `DropItem` leaves a pickable shield (vanilla's drop key; refused during a release / bash, Agent.cs 4446); queue it with
   `Mission.AddTickAction(DropItem, …)` as vanilla's banner bearers do. A broken shield is `RemoveEquippedWeapon` (gone). The AI
   picks up SHIELDS only when no enemy is within 20 m (HumanAIComponent.cs 234, 433).
8. Vanilla's knockdown / knockback resistance already follows the Athletics SKILL (custom battle 0.4 + skill × 0.001).

**The model in one line**: pressure x = the defender's tiredness (1 − f) + 0.25 × the mismatch beyond an even band (both men's
CURRENT points^0.5, the blow's impact - weight, swing speed, blunt - against his guard - shield right / parry / wrong side / none -,
both men's skill) + 0.75 if he is reeling from an impact stagger; each outcome = its ceiling (Anton's 90 / 80 / 80) × an S-curve
of x from its own start (stagger 0, shield 0.2, knockdown 0.35); lengths × (1 + extra × T) with a roll whose spread grows with T;
immunity 2 s / 5 s and halving for 8 s against stun-locks. The worked examples were computed with a script (the model's exact
formulas): two even fresh men always 0; an even man at 25% kicked 82% shield / 70% down; the 200-at-100% king's kick on the
50-at-20% man 90 / 80, the man's on the fresh king 0; the player (100) cannot kick-win against a fresh 220 king (0) but breaks
him at 10% (90 / 80).

**Claude's calls in the plan (Anton can overturn any - IMPACT §8)**: no Harmony; a spike first (I1: one Advanced switch, loud fixed
values, each lever measured in a tournament + a 50v50); the model in SHADOW mode before any effect (I2); shield drop only from
kicks / bashes and wrong-side blocks (0 on a right block); riders knocked off at half the knockdown chance, never a forced
animation on a rider or a horse; the damage term (Anton's unfinished point 12) at 0 until he says what he meant; vanilla's own
knockdowns kept (ours OR-ed on top).

**Next**: Anton answers IMPACT §8 and approves (or reshapes) the build order → TASKS_TODO's BUILD ORDER gets I1… → I1.

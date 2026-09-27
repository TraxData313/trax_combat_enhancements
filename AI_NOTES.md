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

## Step 8 — Squad bars above formations (LATER - Anton, 2026-09-27)

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

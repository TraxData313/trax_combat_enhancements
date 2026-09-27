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
    s.LowFraction(k); s.HighFraction(k); // the ± band, k = FormationSpreadStdDevs, clamped 0..1
}
AthleticsLogic.FormationStatsVersion  // bumps every FormationStatsRefreshSeconds - redraw on change
AthleticsLogic.IsRunning
```
- Formation stats are recomputed by the logic's own tick (one O(N) pass over tracked fighters of
  `Mission.PlayerTeam`, bucketed by `(int)agent.Formation.FormationIndex`), so a view never loops
  agents itself. Enemy formations are not computed (ask if a step needs them).
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
  (it holds attacks itself). A swing ending straight into a ready (a chain) is not held. The
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

## Step 7 — Looked-at NPC bar

- `TargetAthleticsView : TraxHudView` - the recipe above (Step 6, "A new view"). Own raycast
  `Mission.RayCastForClosestAgent` from `MissionScreen.CombatCamera` every `HudRefreshSeconds`
  (inside `ViewConditionMet` or `Refresh` - the frame gives `Player`; read the camera via the
  view's `MissionScreen`), mount → `RiderAgent`, linger `TargetBarLingerSeconds`, range
  `TargetBarMaxDistance` (both already settings). RBM-style place: top centre. Colours, numbers,
  wounded part, marker: `BarMath` exactly as the player bar.
- Read: `AthleticsLogic.TryGetReading(target.IsMount ? target.RiderAgent : target, out var r)`.
- Hidden while `ModEnabled` is off - the base does it (HudGate); the smoke's master-switch step
  already covers the player bar - add the target view there too.

## Step 8 — Squad bars above formations

- `FormationAthleticsView : TraxHudView` (`NeedsPlayer` - decide: can the player command while
  down? probably keep true). Player formations = `PlayerTeam.FormationsIncludingEmpty`,
  `CountOfUnits > 0`, `PlayerOrderController.IsFormationSelectable`. Mean/std: already computed
  by step 5 - `AthleticsLogic.TryGetFormationStats(formation, out var f)`, redraw when
  `FormationStatsVersion` moves (band = `f.LowFraction(FormationSpreadStdDevs)` ..
  `f.HighFraction(…)`). Average HEALTH (DESIGN §3 additions) is not computed yet - add it to the
  same refresh pass (`RefreshFormationStats`) rather than a second loop. Position = `MBWindowManager.WorldToScreen(CombatCamera,
  CachedMedianPosition.GetGroundVec3() + (0,0,FormationBarHeight))`, hide when w < 0.
  Anchor: bind `ScaledPositionXOffset`/`YOffset` (floats - vanilla's GamepadCursor.xml does it;
  step 6 finding) before reaching for an own `Widget` subclass (§G, UNVERIFIED #7). A list VM
  (`MBBindingList`) + `ItemTemplate` for one item per formation. `FormationBarsAlways` off →
  `ViewConditionMet` = markers shown (key 5 held or `Mission.IsOrderMenuOpen`).
- Colour squad bars by `f.MeanPeakShare` with `BarMath.Band`; fill `MeanFraction` (a FillBarWidget).

## Step 9 — Orders menu

- Vanilla cards are generated code — cannot be extended without UIExtenderEx (§G).
  Default plan (implication 1): our own compact per-formation panel while
  `Mission.IsOrderMenuOpen`, inside `FormationAthleticsView` (or a view of its own whose
  `ViewConditionMet` = the orders menu open). UIExtenderEx satellite only
  if Anton insists on numbers inside the cards (and then test with RTS Camera).
- Numbers: `AthleticsLogic.TryGetFormationStats(formation, out var f)` → `f.Describe()`-style
  "72 ± 8" (use `f.MeanPoints` / `f.StdPoints` directly for the strip).
- Hidden while `ModEnabled` is off (the base's HudGate).

## Step 10 — Balance + polish

## Step 11 — Steam packaging

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
EnduranceLogic stub); `module/SubModule.xml` v0.1.0; `tools/deploy.ps1` (build → AssemblyGuard
→ OfflineSmoke → install as `Modules\TraxCombatEnhancements.Dev`); `tools/AssemblyGuard`;
`tools/OfflineSmoke` (16 checks, see below). PLAYTEST §1 written.

**How later steps use it**
- Read settings AT USE TIME: `TraxSettings.Shared.DamageRandomPercent` etc. (typed property per
  key). Never copy into a field at mission start. To rebuild on change: subscribe
  `TraxSettings.Shared.Changed` (raised on the setter's thread, main thread in practice) or
  compare `TraxSettings.Shared.Version`.
- A NEW setting = a row in DESIGN's table + an entry in `SettingsSchema` (right group, right
  place — file and MCM order follow it) + a typed property on `TraxSettings`. `SchemaTests`
  fail until all three agree; the file and the MCM page pick it up by themselves; an old
  config.json gets the new key written in at the next read (logged).
- Log: `TraxLog.Info(tag, msg)`; chatty lines `if (TraxLog.VerboseOn) TraxLog.Verbose(tag, msg)`
  (check VerboseOn FIRST so the hot path builds no strings); `TraxLog.Error("area.hook", e)` in
  every catch. Tags in TraxLog's class doc.
- Summary: `EnduranceLogic.WriteSummary` has the marked spot for step 4/5 lines.
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
  (`OnMissionBehaviorInitialize`, before EnduranceLogic attaches). Missing/invalid key →
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
  0–90, `MaxEndurance` 10–1000. Every setting is `ApplyTiming.Live` (a test enforces it; a
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
   the 1 s retries; the page appears in Mod Options with 7 groups / 32 settings (built and
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

## Step 4 — Damage randomness

- `TraxDamageModel : AgentApplyDamageModel` (decorator): override
  `ApplyGeneralDamageModifiers` only (last step after armor), forward ~30 other members to
  `BaseModel`. Skip: not an agent collider, victim null, shield-blocked, fall damage.
  Ranged = `cd.IsMissile`; mount victim = `ai.IsVictimAgentMount`; horse charge = melee.
  Positive → never below 1 (game rounds). Thread-safe RNG. See RESEARCH §A.
- Core: `DamageRoll` (factor draw, clamp) + roll statistics for the `[summary]`.
- Implications 2–4 (knockdown follows the roll; shields; charge/fall) — confirm with Anton.
- (from step 3) `Models/TraxDamageModel.cs` exists and is registered (`SubModule.RegisterModels`)
  — change only `ApplyGeneralDamageModifiers` (call BaseModel first, wrap the rest in try/catch
  → `TraxLog.Error("damage.roll", e)`, return the base value on error). Settings via
  `TraxSettings.Shared.*` per hit. Summary lines go in `EnduranceLogic.WriteSummary` (marked);
  reset the accumulator at mission start (the damage model outlives missions).

## Step 5 — Endurance core

- `EnduranceLogic : MissionLogic`: per-agent state array by `Agent.Index` (reference-checked),
  built in `OnAgentBuild`, dropped in `OnAgentRemoved`, first-tick sweep. RESEARCH §F.
- Blow detection (§B): poll `GetCurrentActionType(1)` rising edge into `ReleaseMelee`;
  `OnAgentShootMissile` (0.1 s dedupe); `CostOnMiss=false` → `OnMeleeHit` (first per swing) /
  `OnMissileHit`; couched/braced landed hit = one blow. Kicks/bashes free.
- Hero/leader flags cached at spawn (§E), cost computed live per blow.
- Regen in Core; moving = `(MountAgent ?? agent).MovementVelocity` vs threshold (§D).
- Speed (§C): `TraxAgentStatModel : AgentStatCalculateModel` decorator scales
  `SwingSpeedMultiplier`, `ThrustOrRangedReadySpeedMultiplier`, `ReloadSpeed` when
  exhausted; `agent.UpdateAgentProperties()` only on transitions and on config change.
  Forward EVERY member; fix the tournament `SetAILevelMultiplier` swallow.
- MUST measure in game: 0.2 really slows (engine clamp), bows/crossbows too, polling cost.
- (from step 3) `Models/TraxAgentStatModel.cs` exists and is registered; keep `SyncAiLevel()`
  as the FIRST line of `InitializeAgentStats` / `UpdateAgentStats` (tournament fix), scale after
  `BaseModel.UpdateAgentStats`. `Missions/EnduranceLogic.cs` is attached to every SP mission and
  already counts agents built/removed for the summary — grow it, keep every hook wrapped.

## Step 6 — Player bar

- `PlayerEnduranceView : MissionBattleUIBaseView` + own `GauntletLayer` + VM + prefab in
  `module\GUI\Prefabs`. Added by `EnduranceLogic`'s first tick via
  `MissionScreen.AddMissionView` (NOT in OnMissionBehaviorInitialize — §F/§G).
- Layer created/destroyed in `OnMissionScreenTick` on `ShowPlayerBar && !HideBattleUI &&
  combat mode` (hot swap). Place beside the vanilla hero bar (bottom-right, MarginBottom 90 /
  MarginRight 40 in `AgentStatus.xml`); RBM's bars are the style reference (§G).
- (from step 3) The Module already references `TaleWorlds.MountAndBlade.View`, `GauntletUI`,
  `GauntletUI.Data`, `Engine.GauntletUI`, `ScreenSystem`, `InputSystem`; `deploy.ps1` copies
  `module\GUI` into the dev module (folder does not exist yet — create `module\GUI\Prefabs`).

## Step 7 — Looked-at NPC bar

- `TargetEnduranceView`, same pattern. Own raycast `Mission.RayCastForClosestAgent` from
  `MissionScreen.CombatCamera` every `HudRefreshSeconds`, mount → `RiderAgent`, linger.
  Needs the proposed `TargetBarMaxDistance` / `TargetBarLingerSeconds` (implication 7).

## Step 8 — Squad bars above formations

- `FormationEnduranceView`: player formations = `PlayerTeam.FormationsIncludingEmpty`,
  `CountOfUnits > 0`, `PlayerOrderController.IsFormationSelectable`. Mean/std in one O(N)
  pass over our state array (Core math). Position = `MBWindowManager.WorldToScreen(CombatCamera,
  CachedMedianPosition.GetGroundVec3() + (0,0,FormationBarHeight))`, hide when w < 0.
  Anchor via bound `ScaledPosition*Offset` or an own `Widget` subclass (§G, UNVERIFIED #7).
  Implication 8 (`FormationBarsAlways`).

## Step 9 — Orders menu

- Vanilla cards are generated code — cannot be extended without UIExtenderEx (§G).
  Default plan (implication 1): our own compact per-formation panel while
  `Mission.IsOrderMenuOpen`, inside `FormationEnduranceView`. UIExtenderEx satellite only
  if Anton insists on numbers inside the cards (and then test with RTS Camera).

## Step 10 — Balance + polish

## Step 11 — Steam packaging

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

## Step 3 — Scaffold

- Layout per CLAUDE.md. Module references (game `bin` + `Modules\Native\bin`):
  TaleWorlds.Core/Library/Engine/MountAndBlade/CampaignSystem/ScreenSystem/GauntletUI/
  Engine.GauntletUI/InputSystem, `TaleWorlds.MountAndBlade.View` (Native), Newtonsoft.Json
  (game's 13.0.1, `Private=false`). MCMv5.dll via `McmBinFolder` (build only, not shipped).
- SubModule.xml: copy the sibling's (`..\TrainingBattlesMod\module\SubModule.xml`): Native,
  SandBoxCore, Sandbox, StoryMode LoadBeforeThis; `Bannerlord.MBOptionScreen` and `NavalDLC`
  `LoadBeforeThis optional="true"`; add `CustomBattle` optional LoadBeforeThis.
- Config: Core owns schema + defaults + a text writer that puts a `// plain words` line
  above each key (Newtonsoft cannot write `//`; it READS them fine — tested). Module resolves
  the folder via `EngineFilePaths.ConfigsPath` (RESEARCH §H). One shared config object +
  `Version` counter + `Set(key, value, source)` that logs `[config] X: old → new`.
- MCM: FLUENT builder in method bodies only, `ProxyRef<T>` per parameter, `SetFormat("none")`,
  `WithoutDefaultPreset()` + own "Mod defaults" preset, save file on `"SAVE_TRIGGERED"`;
  register from `OnBeforeInitialModuleScreenSetAsRoot`, retry from `OnApplicationTick`
  (RESEARCH §H, §J). Bring over `tools/AssemblyGuard` and make MCMv5 a HARD fail.
- Logging: `trax_combat.log` beside config, tags, 2 MB trim, rate limiter in Core (§K).
- Register the two decorator models in `OnGameStart` already (pass-through), guarded by
  `starter.Models.Any(m => m is T)` — proves the chain works before features land.
- Attach `EnduranceLogic` in `OnMissionBehaviorInitialize` (SP only), log mission start/end.
- Done = loads in game with and WITHOUT MCM; the log shows load, config values, mission
  start/end; PLAYTEST.md §1 written.

## Step 4 — Damage randomness

- `TraxDamageModel : AgentApplyDamageModel` (decorator): override
  `ApplyGeneralDamageModifiers` only (last step after armor), forward ~30 other members to
  `BaseModel`. Skip: not an agent collider, victim null, shield-blocked, fall damage.
  Ranged = `cd.IsMissile`; mount victim = `ai.IsVictimAgentMount`; horse charge = melee.
  Positive → never below 1 (game rounds). Thread-safe RNG. See RESEARCH §A.
- Core: `DamageRoll` (factor draw, clamp) + roll statistics for the `[summary]`.
- Implications 2–4 (knockdown follows the roll; shields; charge/fall) — confirm with Anton.

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

## Step 6 — Player bar

- `PlayerEnduranceView : MissionBattleUIBaseView` + own `GauntletLayer` + VM + prefab in
  `module\GUI\Prefabs`. Added by `EnduranceLogic`'s first tick via
  `MissionScreen.AddMissionView` (NOT in OnMissionBehaviorInitialize — §F/§G).
- Layer created/destroyed in `OnMissionScreenTick` on `ShowPlayerBar && !HideBattleUI &&
  combat mode` (hot swap). Place beside the vanilla hero bar (bottom-right, MarginBottom 90 /
  MarginRight 40 in `AgentStatus.xml`); RBM's bars are the style reference (§G).

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

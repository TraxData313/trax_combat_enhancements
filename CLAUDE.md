# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

**Trax Combat Enhancements** (working title) — a combat mod for *Mount & Blade II:
Bannerlord* v1.4.8 that makes fights a bit more fun: every landed hit rolls ±50% damage,
and every fighter has an **endurance** pool that blows drain and rest refills — empty means
slow attacks. Heroes and party leaders pay less per blow, so the game leans hero-centred.
Endurance is shown for the player, the fighter they look at, and — averaged with a ± spread —
above the player's own formations and in the orders menu.

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
  by hand. Same rule for any other optional dependency.
- **Decorate game models, never subclass `Default*`/`Sandbox*` models.** `AddModel` replaces
  by base type; a subclass silently drops War Sails' and other mods' versions. Extend the
  abstract model and delegate to the previously registered one (sibling lesson). If a model
  cannot carry what we need, Harmony is acceptable — say why in AI_NOTES.
- **Performance**: battles have 500–1000 agents. No per-agent allocations per tick; poll
  what needs polling at a modest rate; UI updates throttled.
- **Save-safe**: the mod lives inside missions. It must add nothing to the campaign save, so
  it can be enabled or removed mid-campaign.

## Layout (planned — make it true in step 3, then keep it true)

```
src/TraxCombat.Core/          netstandard2.0 — pure logic, unit-tested: config schema +
                              defaults, damage roll, endurance math (costs, regen, exhaustion),
                              formation statistics (mean / std)
src/TraxCombat.Module/        net472 — the Bannerlord module: SubModule, config file I/O,
                              MCM bridge (soft), mission behaviors, models, HUD views
tests/TraxCombat.Core.Tests/  net8.0 xUnit — keep green
module/SubModule.xml          manifest; module/GUI/Prefabs for our HUD movies
tools/deploy.ps1              build + install as "Trax Combat Enhancements (dev)"
tools/package.ps1             Steam release layout (step 11)
```

## Build & deploy

```powershell
dotnet build -c Release
dotnet test  -c Release
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1
```

Game path (and MCM path, if referenced) in `Directory.Build.props`; personal overrides in
`Directory.Build.props.user` (git-ignored). The deploy fails while the game runs (DLL lock) —
say so and hand Anton the deploy line.

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

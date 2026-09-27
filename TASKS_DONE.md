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

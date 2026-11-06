# Manager progress — 2026-11-06

Task (Toni): in a shield wall, the men on the two ends of the line should face the flanks (outward), not forward.
Board = TASKS_TODO.md BUILD ORDER step 99 (manager protocol in CLAUDE.md).

## Steps
- [x] 99a. Research: how vanilla's shield wall lays out men and sets facing; how to turn the end files outward per agent; design + settings proposal (AI_NOTES "Step 99"), no code
- [ ] 99b. Implement + tests + docs, build, deploy, commit, push

## Notes
- 99a done: AI_NOTES "Step 99" research section; TASKS_TODO 99 marked [~]. Mechanism: per-man Agent.TrySetFormationFrame with direction formation.Direction.TransformToParentUnitF(-/+Vec2.Side) right after vanilla's ~0.5 s update (step 41 timer), SetDirectionChangeTendency(1) like square/circle men, EnforceShieldUsage(DefendDown). End men = Arrangement.GetNeighborUnitOfLeftSide/RightSide == null. Only held men, shield wall, wall standing (step 75 test). Release forward when enemy within 4 m in front or step 42 frees him. MUST share one "his facing" function with step 68 pace + step 75 pin, else they snap forward. Settings proposed: ShieldWallFlanksFaceOut true, ShieldWallFlankFiles 1, ShieldWallFlankFrontCornerDegrees 45, ShieldWallFlankReleaseMetres 4, + "wall flanks" summary line with angle check.
- Waiting on Toni: corner man 45/90/forward, files per end, standing only or advancing too, shield wall only or line too.

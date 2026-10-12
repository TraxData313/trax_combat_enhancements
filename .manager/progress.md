# Manager progress — 2026-10-12

Task (Anton): fix the tournament 1v1 deadlock (both exhausted, the AI braced and blocked ~95%, Anton
finished him with kicks). Log 2026-10-12 09:19 arena_sturgia_a: no-formation men braced at the "hold" floor
60% - 62% of AI field time, longest 163 s; at empty every swing / paid block restarts the 3 s refill delay
for both sides -> nobody refills. The stagger-animation idea is DROPPED for now (Anton).
Board = TASKS_TODO.md BUILD ORDER 28-30 (project manager protocol in CLAUDE.md).

## Steps
- [x] 28. Brace floor for no formation 20% (BraceFloorNoFormationPercent) + per-man floor roll ±10 (BraceFloorSpreadPercent, independent of the ±5 recover roll), everywhere
- [x] 29. Only wrong-side shield blocks restart the refill delay (one switch); right-side + parries still cost
- [x] 30. Tired AI block direction: mix his own pick with a random one by weight setting × (1 − f), once per incoming attack; research the input direction bits first

- [~] 31. Player-facing docs: tight Steam description + README, new docs/ player guide with tables (Anton 2026-10-12)

## Notes
- Release: fixes collect in main; version / upload = Anton's call later.
- 28 done (6f2ffc6..83fd903): BraceFloorNoFormationPercent 20, BraceFloorSpreadPercent 10 (own dice, live). 487 tests, smoke 69. Assumes tournament men read as "no formation" (PLAYTEST D8 checks).
- 29 done (5289cfd..ab24f63): OnlyWrongSideBlocksStopRefill true; AthleticsMath.BlockRestartsRefill. 498 tests, smoke 70. Parrying from empty every 1.5 s: soldier climbs only with pool > 80, hero > 60, hero+leader always; right-side shield blocks always climb. Anton's call whether small-bar parriers should climb (levers: CostPerWeaponParry, FullRegenSecondsStanding).
- Deploy on this PC (scripts default to D:, which does not exist):
  powershell -ExecutionPolicy Bypass -File tools\deploy.ps1 -GameFolder "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord" -McmBinFolder "C:\Program Files (x86)\Steam\steamapps\workshop\content\261550\2859238197\bin\Win64_Shipping_Client"
- 30 done (4de175f..256ca1c): TiredBlockRandomPercent 100 via the game stat AiRandomizedDefendDirectionChance (DrivenProperty 40) in the stat decorator - chance = own + (1-own) x w, w = setting x (1-f); NOT the input bits (fallback designed in AI_NOTES). 514 tests, smoke 72. Unverified in game: PLAYTEST D8 step 5 ("AI guard by f" bands should differ).

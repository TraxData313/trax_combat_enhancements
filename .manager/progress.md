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

- [x] 31. Player-facing docs: tight Steam description + README, new docs/ player guide with tables (Anton 2026-10-12)

Phase 2 (Anton 2026-10-12):
- [x] 32. Swing stack: max(2, round(points/20)) stored swings, one back per pause, kicks/bashes use one, pips on the recovery bar
- [~] 33. Sprint: key, bar = points/20 s, min 20% to start, 1.6x current for the first 30% of the sprint time (slider, default 30 - Anton) then linear to 1.1x, refill 7 s (x0.5 moving, x0.5 exhausted), no Athletics refill while sprinting, AI on new orders (charge all, move/advance 50%), foot only

## Notes
- Release: fixes collect in main; version / upload = Anton's call later.
- 28 done (6f2ffc6..83fd903): BraceFloorNoFormationPercent 20, BraceFloorSpreadPercent 10 (own dice, live). 487 tests, smoke 69. Assumes tournament men read as "no formation" (PLAYTEST D8 checks).
- 29 done (5289cfd..ab24f63): OnlyWrongSideBlocksStopRefill true; AthleticsMath.BlockRestartsRefill. 498 tests, smoke 70. Parrying from empty every 1.5 s: soldier climbs only with pool > 80, hero > 60, hero+leader always; right-side shield blocks always climb. Anton's call whether small-bar parriers should climb (levers: CostPerWeaponParry, FullRegenSecondsStanding).
- Deploy on this PC (scripts default to D:, which does not exist):
  powershell -ExecutionPolicy Bypass -File tools\deploy.ps1 -GameFolder "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord" -McmBinFolder "C:\Program Files (x86)\Steam\steamapps\workshop\content\261550\2859238197\bin\Win64_Shipping_Client"
- 30 done (4de175f..256ca1c): TiredBlockRandomPercent 100 via the game stat AiRandomizedDefendDirectionChance (DrivenProperty 40) in the stat decorator - chance = own + (1-own) x w, w = setting x (1-f); NOT the input bits (fallback designed in AI_NOTES). 514 tests, smoke 72. Unverified in game: PLAYTEST D8 step 5 ("AI guard by f" bands should differ).
- 31 done (40c9c35..f604f85): Steam description 3042 bytes, README same shape, docs/HOW-IT-WORKS.md (14 sections, tables from defaults.json). Not uploaded - Anton decides the next Workshop update.
- ALL STEPS DONE 2026-10-12. Next: Anton playtests PLAYTEST D8 (tournament), then decides a release.
- 32 done (d54238c..2a058db): SwingStackEnabled/PointsPerSwing 20/Minimum 2/KicksUseSwings. 531 tests, smoke 75. Deviation: the AI's own ~1 s gap (AiMeleeGapSeconds) is not counted as rest, so battle pace holds (DESIGN decision 30e). Player kick gated via EventControlFlag.Kick 0x8000; AI kick bit unverified. Bar label "Swings 4/8".
- Tournament 12:34 log (steps 28-31 build): tired-direction chance rose to 1.00 at empty; with SHIELDS a wrong guess = wrong-side block (63% at empty), landed only 6% - shields still stop blows. Shieldless parries unproven (44 total). Proposed to Anton: split "AI guard by f" by shield / no shield (waiting for his yes); design question: should a tired man's wrong-side shield block leak damage.

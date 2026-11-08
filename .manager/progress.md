# Manager progress — 2026-11-08

Task (Toni): timed blocks. A correct shield block / weapon parry made just as the blow lands
staggers the attacker longer - up to 2 s for a nicely timed one - so blocking becomes a skill.
Against a mounted charge, a correct, well-timed block can bring the attacker down. The blocker
should visibly push the shield / weapon out as it lands (now he just sits there while the attacker
reels). A short slow-mo when the PLAYER times one nicely.
Board = TASKS_TODO.md BUILD ORDER step 100 (manager protocol in CLAUDE.md).

## Steps
- [x] 100a. Research + design (AI_NOTES "Step 100"), no code: how to measure block timing; how it plugs into the existing Impact / riposte stagger (step 56); 2 s stagger; mounted attacker down; blocker push animation; slow-mo API; settings proposal
- [x] 100b. Core: timed-block window + longer riposte stagger (shield + parry), settings, tests, build, deploy, commit, push
- [-] 100c. Mounted charge - SKIPPED (Toni 2026-11-08: 'skip the mounted part'); the half-done local work was dropped, never pushed
- [x] 100e. Player slow-mo on a well-timed block (moved ahead of the animation)
- [x] 100d. Blocker push-out animation on a timed block
- [x] 100g. PERFECT STRIKE (Toni 2026-11-08, added while 100d ran): after the player's timed block staggers the attacker, the player's counter-swing from the OPPOSITE side (blocked his right attack coming from my left -> I swing from my right to left) landing while he is still staggered does 2-3x damage + an on-screen 'PERFECT STRIKE'. Manager defaults: player only; 2x at score 0 rising to 3x at score 1 (the block's timing score); one perfect strike per stagger; only while the riposte stagger runs. Add the TASKS_TODO line when building (not added yet, to avoid clashing with the 100d agent).
- [ ] 100f. Independent review + fixes, PLAYTEST entry, close step 100

## Notes
- 100a done (4fe60bf): AI_NOTES 'Step 100' + DESIGN decision 91 (PROPOSED). Order now b, c, e, d, f. Game is at C:\Program Files (x86)\Steam\...\Bannerlord v1.4.8 (not the D:\ path in Directory.Build.props).
  Design: per-man guard clock; score 1 within 0.15 s of the hit -> 0 at 0.5 s; player auto-block mode: only the press counts; AI read at contact from channel-1 defend action, score x0.5. Riposte chance blends step 56 -> 85 %, length -> 2.0 s (1.2 s cap against Toni); score 0 = step 56 exactly. ImpactMath.Decide.
  Rider: score >= 0.5, foot blocker, existing dismount blow; 40 % x score^2 x (weapon 0.5) x horse speed (0.25..1) x skill; else horse rears (separate lever); never vs couched lance.
  Slow-mo: game time-speed request 0.3, eased back over 0.5 s real, 4 s cooldown, player only, always removed on every exit.
  Animation: act_shield_bash / act_1h|2h|staff_bash upper body, skip windup, ~0.25 s; fallback the guard's _parry_heavy; test tool first.
  Open questions answered by the manager with the recommended defaults (Toni said 'go'): AI times blocks at half strength (cap 1.2 s vs Toni); 0.15 s perfect / 0.5 s none; horse rears if rider stays on; push-out on with a switch. Tell Toni in the final summary.
- 100b done (043c896, f6311b1, 4bbe346): Core TimedBlock.cs (GuardClock, TimedBlockMath, TimedBlockResult, TimedBlockStats); ImpactMath.Decide blend; Module GuardBody.cs, AthleticsLogic.TimedBlock.cs; 1408 tests, smoke 217; deployed; PLAYTEST D64. TimedBlocksEnabled + 7 Advanced keys.
  For 100c: a correct block vs a rider has TimedScore + TimedBlockResult.AttackerMounted but riposte still refused as Rider - add dismount/rear roll next to it in Decide (d.AttackerSpeed, d.XRiposte in the same branch). 100d/100e hook TimedRiposteAccepted(attacker, blocker, score, now) called from the tick when the engine takes the stagger.
  Unverified: Parried vs Blocked mapping (D64 summary line), 2 s stagger in game.
- 100c SKIPPED by Toni. Riders keep 100b behaviour (riposte refused as Rider). 100d/100e must not touch riders.
- AI timing: Toni first asked it off, then back on after hearing 100b doesn't change AI behaviour -> TimedBlockAiPercent stays 50 (nothing committed for it).
- 100e done (80d1d2a, d7af0d0, d44ab4d): Core TimedSlowMo.cs, Module SlowMoBody.cs + AthleticsLogic.TimedSlowMo.cs; player score >= 0.8, request id 31100, 0.3 -> 1 over 0.5 s real (ApplicationTime), 4 s cooldown; removed on every exit; 1420 tests, smoke 219; deployed (no rider code); PLAYTEST D66. Rider part marked DROPPED in DESIGN 91 / AI_NOTES.
  For 100d: call the push-out beside MaybeStartSlowMo in TimedRiposteAccepted, own try. Animations run on battle time: during the 0.3 floor a push-out looks ~3x slower.
- OPEN for 100d: asked Toni whether to add direction-matched attacker recoils (game has act_blocked_<slashleft|slashright|overswing|thrust>_<1h|2h|staff|lance|fist>[_left_stance], quick_ variants) before the backward stagger. Currently one act_stagger_backward. -> Toni 2026-11-08: YES, add the direction-matched recoils to 100d.
- 100d done (bc0996f, fb51102, a990f64): Core TimedAnim.cs, Module AthleticsLogic.TimedAnim.cs + AnimBody.cs. Push-out = upper-body bash from 30%, 0.25 s, any blocker (not riders), fallback _parry_heavy; 0.5 s window where the pusher's bash collision is no blow (damage/knockback/our systems ignore). Recoil act_blocked_<dir>_<weapon> slowed to min(0.45 s, 50%) then backward stagger; untimed ripostes unchanged. Test tool: Advanced/Impact 'animation test tool', J / LShift+J, 26 candidates. 1431 tests, smoke 221, deployed. Push-out min score 0 (not 0.5). Flaky: Step91Tests/Step95Tests no-allocation checks failed once each - 100f should look.
- 100g done (053c7ec, 8a13bf3, 8d3cf2e): Core PerfectStrike.cs, Module AthleticsLogic.PerfectStrike.cs + hooks in ImpactHooks/DamageRandomizer/TimedBlock. Window = ReelingUntil + 0.15 s; opposite side = SAME engine AttackDirection value (attacker-relative); overhead -> either side; thrust/unknown -> any non-thrust swing; Toni's thrust never. x2..x3 applied last in DamageRandomizer. Gold feed + centre 'PERFECT STRIKE xN'. 1445 tests, smoke 222, deployed. Step91/95 did not flake in 3 runs.

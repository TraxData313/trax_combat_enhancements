# Manager progress — 2026-11-08

Task (Toni): timed blocks. A correct shield block / weapon parry made just as the blow lands
staggers the attacker longer - up to 2 s for a nicely timed one - so blocking becomes a skill.
Against a mounted charge, a correct, well-timed block can bring the attacker down. The blocker
should visibly push the shield / weapon out as it lands (now he just sits there while the attacker
reels). A short slow-mo when the PLAYER times one nicely.
Board = TASKS_TODO.md BUILD ORDER step 100 (manager protocol in CLAUDE.md).

## Steps
- [x] 100a. Research + design (AI_NOTES "Step 100"), no code: how to measure block timing; how it plugs into the existing Impact / riposte stagger (step 56); 2 s stagger; mounted attacker down; blocker push animation; slow-mo API; settings proposal
- [ ] 100b. Core: timed-block window + longer riposte stagger (shield + parry), settings, tests, build, deploy, commit, push
- [ ] 100c. Mounted charge: a timed correct block can unhorse / knock down the rider
- [ ] 100e. Player slow-mo on a well-timed block (moved ahead of the animation)
- [ ] 100d. Blocker push-out animation on a timed block
- [ ] 100f. Independent review + fixes, PLAYTEST entry, close step 100

## Notes
- 100a done (4fe60bf): AI_NOTES 'Step 100' + DESIGN decision 91 (PROPOSED). Order now b, c, e, d, f. Game is at C:\Program Files (x86)\Steam\...\Bannerlord v1.4.8 (not the D:\ path in Directory.Build.props).
  Design: per-man guard clock; score 1 within 0.15 s of the hit -> 0 at 0.5 s; player auto-block mode: only the press counts; AI read at contact from channel-1 defend action, score x0.5. Riposte chance blends step 56 -> 85 %, length -> 2.0 s (1.2 s cap against Toni); score 0 = step 56 exactly. ImpactMath.Decide.
  Rider: score >= 0.5, foot blocker, existing dismount blow; 40 % x score^2 x (weapon 0.5) x horse speed (0.25..1) x skill; else horse rears (separate lever); never vs couched lance.
  Slow-mo: game time-speed request 0.3, eased back over 0.5 s real, 4 s cooldown, player only, always removed on every exit.
  Animation: act_shield_bash / act_1h|2h|staff_bash upper body, skip windup, ~0.25 s; fallback the guard's _parry_heavy; test tool first.
  Open questions answered by the manager with the recommended defaults (Toni said 'go'): AI times blocks at half strength (cap 1.2 s vs Toni); 0.15 s perfect / 0.5 s none; horse rears if rider stays on; push-out on with a switch. Tell Toni in the final summary.

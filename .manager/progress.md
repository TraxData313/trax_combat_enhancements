# Manager progress — 2026-11-08

Task (Toni): timed blocks. A correct shield block / weapon parry made just as the blow lands
staggers the attacker longer - up to 2 s for a nicely timed one - so blocking becomes a skill.
Against a mounted charge, a correct, well-timed block can bring the attacker down. The blocker
should visibly push the shield / weapon out as it lands (now he just sits there while the attacker
reels). A short slow-mo when the PLAYER times one nicely.
Board = TASKS_TODO.md BUILD ORDER step 100 (manager protocol in CLAUDE.md).

## Steps
- [ ] 100a. Research + design (AI_NOTES "Step 100"), no code: how to measure block timing; how it plugs into the existing Impact / riposte stagger (step 56); 2 s stagger; mounted attacker down; blocker push animation; slow-mo API; settings proposal
- [ ] 100b. Core: timed-block window + longer riposte stagger (shield + parry), settings, tests, build, deploy, commit, push
- [ ] 100c. Mounted charge: a timed correct block can unhorse / knock down the rider
- [ ] 100d. Blocker push-out animation on a timed block
- [ ] 100e. Player slow-mo on a well-timed block
- [ ] 100f. Independent review + fixes, PLAYTEST entry, close step 100

## Notes

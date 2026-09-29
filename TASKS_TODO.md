BUILD ORDER (one step at a time — [~] = in flight, see CLAUDE.md "manager protocol"):
- [x] 1. Repo, docs, design spec, public GitHub
- [x] 2. Research: verify every hook against the 1.4.8 game → docs/RESEARCH.md (no code) (see AI_NOTES)
- [x] 3. Scaffold: solution, Core + tests, module, config file with instructions, MCM (optional), logging, deploy — loads in game, does nothing yet
- [x] 4. Damage randomness ±50% — first playable
- [x] 5. Endurance core: blow costs, hero/leader multipliers, regen, 20% speed when empty
- [x] 5b. Rename Endurance → ATHLETICS everywhere (UI, keys, code, docs) + defaults.json — all defaults in one file I edit and push; MCM button reverts to it (DESIGN §2b, §2c) + master switch: whole mod off, live (DESIGN §4)
- [x] 5c. Athletics v2: pool = Athletics skill (floor 50, a slider), 10 pts a blow, top 75–100% of the bar = full strength (green), health caps the pool, damage upside + swing + run speed fall below the peak, regen by effort (DESIGN §2b)
- [x] 5d. Tired AI fighters step back after melee swings — fresh ones take their place (DESIGN §2)
- [x] 6. Player Athletics bar (RBM posture style) — number, 75% peak marker, green/blue/yellow/orange/red
- [x] 5e. Attack speed = the whole attack RATE (wind-up + swing + recovery + AI pause): 0.5 → one attack per 2 s instead of 1 s (DESIGN §2) — right after 6
- [x] 9. Orders menu: strip under the formation cards (below the arrows) — Athletics ± spread + health (no UIExtenderEx) — kept by Anton: "some vision of the state of the troops"
- [x] 10a. Fresh-eyes code review: bugs, crash paths, stuck states, performance, gates — fix what is found
- [x] 10b. Polish: hide the LATER features' settings (no switch that does nothing), settings/config readability, docs + PLAYTEST script as one clean run (Anton's playtest comes after, all at once)
- [x] 11. Steam packaging — upload only on Anton's yes (package ready — upload waits for Anton's yes after the playtest)
- [x] 12. Playtest fixes, round 1 (see BUGS) — bar outside battles, MCM stops cleanly; exit hang not ours (see AI_NOTES)
- [x] 13. PAUSE ONLY (Anton's playtest call): no slow-mo, a no-attack timer after each attack (you + AI), countdown "1.3 s" by the bar, bar flashes if you swing too early (DESIGN §2) — + "Attack recovery" bar ABOVE the Athletics bar: empties on attack, refills over the pause, secs inside, flashes on an early press (Anton, 2026-09-27) — built: your attack button waits (hold it = attack when full), AI waits guard up (melee, ranged, riders); AI "decide less" now off; your Athletics row moved down to make room (see AI_NOTES)
- [x] 14. GO (Anton, night of 2026-09-27: "make them slow down to 70% speed") — run-speed floor 0.3 → 0.7 · refill faster when low, slower when full: straight line, rate near full = 50% of rate near empty (slider; 100 = the flat refill he played), empty→full still 60 s at a walk — after 15
- [x] 16. Step back that WORKS + the GUARD REALLY UP (BATTLE_PACING lever #2 = a spec bug fix: DESIGN says blocking is never held): a per-man AgentComponent on OnAIInputSet clears only the attack bits during the timer (replaces NoAttack, which also killed their blocking), walks the step back BACKWARDS facing the enemy via the AI's own input, and keeps the timer through a step back (see BUGS) — overnight, no questions (Anton); the other levers wait for Anton's pick
- [x] 15. Research (no code): how RBM makes battles longer and more tactical, WITHOUT its unit overhaul — a menu of levers (+ our own ideas) for Anton to pick; 80v80 infantry ended in 4–5 min (Anton, 2026-09-27); + how RBM steps a man back FACING his enemy (ours turn their backs — see BUGS)
- [x] 17. Second fresh-eyes review: the ~4000 lines since step 10a's review (steps 12–16: player input timer, AI input component, backpedal, refill curve, migrations) — fix what is found, before Anton's morning playtest
- [x] 19. Hideout boss fight = a fresh start: when the boss (+ friends) fight begins — duel OR men-to-men — the player's side refills to full Athletics (Anton, 2026-09-28: "they come fresh and we will be tired")
- [x] 18. Kicks and shield bashes cost 3 Athletics (Anton, 2026-09-28) — a slider, hero/leader multipliers apply like any blow
- [x] 20. Hold ALT → Athletics + health under each vanilla formation marker (beside its count + distance) (Anton, 2026-09-28) — LATER #8 revived in its "only while vanilla shows the markers" form — built: shows exactly with the game's markers (ALT or the orders menu), yours + the enemy's, markers read live (see AI_NOTES)
- [x] 20b. Anton's tuned defaults (2026-09-28): run floor 0.7 → 0.6 at empty · swing ANIMATION slowed in a straight line to 85% at empty (full speed at the peak line, like the run speed - today it is max(m, floor), which would hit 85% almost at once), the pause in seconds stays exactly as it is · config.json migrated once (format 4) — after 20, before 21 — built: the line by f, D at full animation speed (pause unchanged), format 4; your config.json keeps your own 0.5 / 90 → MCM "Revert all to defaults" (see AI_NOTES)
- [x] 21. Slower, more defensive fights (Anton, 2026-09-28): AI infantry with a SHIELD in hand swing ~30% less (slider; foot men without a shield 15%, own slider) · AI archers wait an extra N seconds after each shot, sized to fire ~30% slower (seconds slider; crossbows their own) — AI only, through the existing pause (guard up), on top of tiredness; the summary measures the real swings/shots per minute — after 20 — built: class read from what he holds; the shield wall's wait sized on his whole rhythm (the log: a pause adds to his own ~1 s gap - a D-only share gave 16%, not 30%); bows +2.0 s, crossbows +2.5 s; A/B it in PLAYTEST D5 / D6 (see AI_NOTES)
- [~] 22. Anton's tuning + defending costs (2026-09-29): refill back to a straight line (RegenRateNearFullPercent 50 → 100, 60 s at a walk) · run floor 0.6 → 0.3 · a blocked melee blow costs the DEFENDER: shield on the right side 1, shield on the wrong side 5, weapon parry (no shield) 2 - hero/leader multipliers apply, you too · config.json migrated once
- [ ] 23. Brace by orders (Anton, 2026-09-29): an AI man whose bar drops to his order's floor stops attacking and only defends (guard up) until he refills to floor + 20 - hold / halt / retreat / move / follow / fall back 60%, advance 40%, charge 20% (all sliders) · while bracing he takes out his shield if he carries one — after 22

PLAYTEST (Anton, all at once at the end — script in docs/PLAYTEST.md):
- [ ] Play with RBM disabled — declared NOT compatible (Anton, 2026-09-27; the mod warns if it sees it)
- [ ] The same custom battle with the mod ON and OFF, compare the two summaries (PLAYTEST §4)
- [ ] Tune defaults in game, "Save current values as a defaults file", hand it to Claude (PLAYTEST §5)
- [ ] Anton: playtest, then say yes → first Workshop upload (tools/WORKSHOP-UPLOAD.md)

LATER (moved off the build order by Anton, 2026-09-27 — designs kept in DESIGN §3 + AI_NOTES steps 7–9):
- [ ] 7. Bar for the NPC I look at (toggle)
- [x] 8. Squad bars above my formations: average ± 1 std + average health → built as step 20 (ALT)

BUGS:
- [x] Loading screen showed "0.1.0+<40-char commit>" (Anton's screenshot, 2026-09-28) — players see "0.1.0" now, the log keeps the full id (29976a7; deploys when the game closes)

PLAYTEST RESULTS (200v200 infantry, 2026-09-28 morning, step 16 in game — 0 errors):
- Step back fixed: facing mid-step 87% (was 12%), back turned 2% (was 80%), 1.4 m moved (was 0.6), blocks 45% (was 5%)
- Guard fixed: held men block 52% (was 2–13%) — now ABOVE everyone else's 30%; if tired men turtle too much: AiHoldRaiseGuard off
- Run floor ×0.70 honoured; the attack timer held (0–1 early starts in ~2400); tick cost 0.055 ms at 400 men
- Kicks/bashes: none seen in that battle — kick a few times next battle to prove the cost (PLAYTEST C1c)
- [x] Tired AI men are DEFENCELESS (step 15's find, log 23:00): while the attack timer holds them (engine NoAttack) they block 2–13%, while stepping back 4–6%, vs 33–45% for everyone — 41% of landed melee hits struck men in those states → step 16 — fixed in 16: only the attack bits taken out of the AI's own input, guard raised (the summary's GUARD line checks it)
- [x] Step back turns their backs (Anton saw it twice; log 22:48 + 23:00 confirms): 100% face the enemy at the start, ~80% back turned mid-step, only ~0.6 m of 2 m moved, block 5% vs 33–45% — the scripted "go to" walk turns them around → step 16 (after 15's research + 14) — fixed in 16: a backpedal through his own input, facing the enemy
- [x] Empty AI attacks too fast (log: f 0 cycle 2.2 s vs target 8.5–9.3 s): at empty the step back fires every swing and a started step back DROPS the pace hold (R1's rule) — so after 1.5 s he swings again → step 16: the timer must survive a step back — fixed in 16: the timer survives a step back (the later end)

- [x] No Athletics bar in the training field (Anton, 2026-09-27) — it runs in walk-around mode, the bar only showed in battle/duel/tournament/stealth; show it outside battles too when a weapon is drawn or the bar isn't full — fixed in step 12: weapon or shield in hand, or refilling; new switch "Your bar outside battles too"
- [x] MCM retried "not ready" every second all session when MCM wasn't enabled but another mod carried its DLL (log 21:08) — stop cleanly — fixed in step 12: MCM's module off = one line, no tries; retries capped at 30
- [ ] Game hangs on "shutting down" in Steam after exit (Anton) — NOT our mod by its log (it logged "unloaded (game closing)" 4 s after the mission, it runs no threads); suspects: another mod's helper process/thread, the game's Watchdog.exe; next time it hangs, tell Claude while it hangs — the process tree shows who is still alive (see AI_NOTES step 12)

NOT DECIDED (defaults in place, Anton can flip):
- [ ] Hero multiplier: 0.75 (default now) or 0.5? (see DESIGN interpretations)
- [x] Kicks / shield bashes cost Athletics? → yes, 3 (Anton, 2026-09-28) → step 18
- [x] RTS Camera's AI driving your hero: no attack pause for him → leave it (Anton, 2026-09-28; REVIEW R-"For Anton")
- [ ] Athletics in tournaments / arena too? (on for now)
- [ ] First public version: v0.1.0 (now) or v1.0.0? (stamped once, in module/SubModule.xml)

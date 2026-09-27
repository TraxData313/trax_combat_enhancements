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
- [ ] 14. ON HOLD (Anton: "don't fix anything" after a battle that felt right, 2026-09-27) — Refill faster when low, slower when full: straight line, rate near full = 50% of rate near empty (slider), empty→full still 60 s at a walk · run-speed floor 0.3 → 0.7 (Anton: "too slow, unrealistic")
- [~] 15. Research (no code): how RBM makes battles longer and more tactical, WITHOUT its unit overhaul — a menu of levers (+ our own ideas) for Anton to pick; 80v80 infantry ended in 4–5 min (Anton, 2026-09-27); + how RBM steps a man back FACING his enemy (ours turn their backs — see BUGS)

PLAYTEST (Anton, all at once at the end — script in docs/PLAYTEST.md):
- [ ] Play with RBM disabled — declared NOT compatible (Anton, 2026-09-27; the mod warns if it sees it)
- [ ] The same custom battle with the mod ON and OFF, compare the two summaries (PLAYTEST §4)
- [ ] Tune defaults in game, "Save current values as a defaults file", hand it to Claude (PLAYTEST §5)
- [ ] Anton: playtest, then say yes → first Workshop upload (tools/WORKSHOP-UPLOAD.md)

LATER (moved off the build order by Anton, 2026-09-27 — designs kept in DESIGN §3 + AI_NOTES steps 7–9):
- [ ] 7. Bar for the NPC I look at (toggle)
- [ ] 8. Squad bars above my formations: average ± 1 std + average health

BUGS:
- [ ] Step back turns their backs (Anton saw it; log 22:48 + 23:00 confirms): 100% face the enemy at the start, ~80% back turned mid-step, only ~0.6 m of 2 m moved, block 5% vs 33–45% — the scripted "go to" walk turns them around. NOT fixing yet (Anton) — step 15 researches how RBM does it
- [x] No Athletics bar in the training field (Anton, 2026-09-27) — it runs in walk-around mode, the bar only showed in battle/duel/tournament/stealth; show it outside battles too when a weapon is drawn or the bar isn't full — fixed in step 12: weapon or shield in hand, or refilling; new switch "Your bar outside battles too"
- [x] MCM retried "not ready" every second all session when MCM wasn't enabled but another mod carried its DLL (log 21:08) — stop cleanly — fixed in step 12: MCM's module off = one line, no tries; retries capped at 30
- [ ] Game hangs on "shutting down" in Steam after exit (Anton) — NOT our mod by its log (it logged "unloaded (game closing)" 4 s after the mission, it runs no threads); suspects: another mod's helper process/thread, the game's Watchdog.exe; next time it hangs, tell Claude while it hangs — the process tree shows who is still alive (see AI_NOTES step 12)

NOT DECIDED (defaults in place, Anton can flip):
- [ ] Hero multiplier: 0.75 (default now) or 0.5? (see DESIGN interpretations)
- [ ] Kicks / shield bashes cost Athletics? (free now)
- [ ] Athletics in tournaments / arena too? (on for now)
- [ ] First public version: v0.1.0 (now) or v1.0.0? (stamped once, in module/SubModule.xml)

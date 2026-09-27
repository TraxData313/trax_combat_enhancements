BUILD ORDER (one step at a time — [~] = in flight, see CLAUDE.md "manager protocol"):
- [x] 1. Repo, docs, design spec, public GitHub
- [x] 2. Research: verify every hook against the 1.4.8 game → docs/RESEARCH.md (no code) (see AI_NOTES)
- [x] 3. Scaffold: solution, Core + tests, module, config file with instructions, MCM (optional), logging, deploy — loads in game, does nothing yet
- [x] 4. Damage randomness ±50% — first playable
- [x] 5. Endurance core: blow costs, hero/leader multipliers, regen, 20% speed when empty
- [ ] 5b. Rename Endurance → ATHLETICS everywhere (UI, keys, code, docs) + defaults.json — all defaults in one file I edit and push; MCM button reverts to it (DESIGN §2b, §2c)
- [ ] 5c. Athletics v2: pool = Athletics skill (floor 50, a slider), 10 pts a blow, top 75–100% of the bar = full strength (green), health caps the pool, damage upside + swing + run speed fall below the peak, regen by effort (DESIGN §2b)
- [ ] 5d. Tired AI fighters step back after melee swings — fresh ones take their place (DESIGN §2b)
- [ ] 6. Player Athletics bar (RBM posture style) — number, 75% peak marker, green/blue/yellow/orange/red
- [ ] 7. Bar for the NPC I look at (toggle)
- [ ] 8. Squad bars above my formations: average ± 1 std + average health
- [ ] 9. Orders menu: strip under the formation cards (below the arrows) — endurance ± spread + health (no UIExtenderEx)
- [ ] 10. Self-review + polish pass (Anton's playtest comes after, all at once)
- [ ] 11. Steam packaging — upload only on Anton's yes

PLAYTEST (Anton, all at once at the end — script in docs/PLAYTEST.md):
- [ ] Play with RBM disabled — declared NOT compatible (Anton, 2026-09-27; the mod warns if it sees it)

BUGS:

NOT DECIDED (defaults in place, Anton can flip):
- [ ] Hero multiplier: 0.75 (default now) or 0.5? (see DESIGN interpretations)
- [ ] Kicks / shield bashes cost endurance? (free now)
- [ ] Endurance in tournaments / arena too? (on for now)

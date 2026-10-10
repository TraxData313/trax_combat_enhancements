# Manager progress — 2026-10-10

Task (Anton): add a configurable overall damage reduction (default 25%: an average 100 swing → 75), on all
sources, to slow combat further. Then prep the mod for launch, modelled on ..\better-skirmisher-separation
(its launch prep: Steam description, thumbnail/cover scripts, workshop files, README). Anton is taking
screenshots meanwhile. Pitch: slower combat, tactics matter more, heroes a bit stronger and key figures.
Upload stays Anton's yes.

## Steps
- [x] 25. Damage reduction (DamageReductionPercent 25, all sources, live, master switch) — full protocol
- [ ] 26. Launch prep like better-skirmisher-separation (research its launch kit, adapt: Steam page, preview/cover, README, screenshot slots, package dry run) — no upload
- [ ] 27. Final check + summary to Anton (open questions: version v0.1.0 vs v1.0.0)

## Notes
- 25 done (442cfb7..01f681f): DamageReductionPercent 25 (0-90), Core DamageReduction.cs, after the roll; all hits on people/horses/shields, not falls/objects, not with mod off. 482 tests, smoke 68 OK. Deploy: agent said D: not mounted, used C: Steam paths, install copy hit a locked DLL -> Anton redeploys with the game closed. Open: shields reduced too (no toggle).
- Anton mid-step: took screenshots with F12 (Steam screenshots); wants a GOOD thumbnail + description in readable bits; pitch: slows combat down WITHOUT the overhaul of "that popular combat mod" (RBM: spears, troop rework).

# Code review — step 10a (2026-09-27)

A fresh-eyes review of the whole mod (src/Core + src/Module, with tests/ and tools/ as evidence)
before Anton's one-session playtest. Every finding was checked against the code around it and,
where the game was involved, against the decompiled v1.4.8 source (`..\reference\game-decompiled\`)
before anything was changed. Order of the review: (1) what can break a player's game, (2) stuck
states, (3) correctness against DESIGN.md, (4) performance at 1000 agents, (5) logging for the
one-shot playtest.

**Tally**: 0 blockers, 1 major (fixed), 9 minors (7 fixed, 2 deferred), 1 "For Anton", 13 checked
and not a bug. Fixes: 4 commits. R1, R2 and R5 have OfflineSmoke checks that FAIL without the fix
(proved by reverting it: 8 failures); R6 has a smoke step for the new log writer; R3, R4, R18 and
R19 sit on teardown / first-tick paths the offline smoke cannot reach (no Mission object) - read
and built only. No Core unit test was possible: every fix is module bookkeeping (the smoke drives
the real logic). Tests 288 (unchanged), OfflineSmoke 42 → 44 steps.

Severity: **blocker** = crashes or corrupts a game; **major** = a feature silently does not do
what DESIGN says, or a stuck state; **minor** = an edge case, a cost, a log gap.

---

## Findings

| Id | Severity | Where | What can happen | Status |
|---|---|---|---|---|
| R1 | major | `Missions/AthleticsLogic.AttackRate.cs:338` (`PaceSwingEnded`), `:476` (`TickPace`) | A tired AI swing ends → the step-back roll says yes (at f 0 it always does) → the pace hold is skipped because a step back is PENDING → the tick REFUSES the step back (`StepBackMaxAtOnce` 50 reached, shield wall, no enemy within 4 m, spot not level…) → the man is neither stepping back nor held. In a big battle past the cap that is most tired men: T3 (DESIGN §2 attack rate) silently off, and the summary would read "too fast" and blame the technique. | FIXED in dc2fe8d - the hold is queued anyway; `TickPace` (after `TickStepBacks` in the same tick) drops it only for a man whose step back STARTED (counted "stepping back"). Smoke: a refused step back is held, a started one is not. |
| R2 | minor | `AthleticsLogic.AttackRate.cs:522` (`StartHold`) | A hold ended while a game job was on him (he stays listed, "waiting"); his next swing's hold starts before the waiting pass → he is in `_paceHeld` twice → the tick ends that hold twice and the summary counts it twice. No stuck flag. | FIXED in dc2fe8d - a waiting man is not listed again. Smoke: listed once, ends once. |
| R3 | minor | `Missions/AthleticsLogic.cs:236` (`OnRemoveBehavior`) | `StopAthletics()` sat in the same try as the fallback summary: an exception there left the dead mission as `AthleticsLogic.Current` for the decorators, the HUD and `Failed()` (whose once-per-site log would then swallow the next mission's first errors). | FIXED in 0558f05 - `StopAthletics()` in a `finally`. |
| R4 | minor | `AthleticsLogic.cs:255` (`WriteSummary`) | The once-flag is set, then the agents on the field are counted OUTSIDE any try - an exception there lost the whole battle summary. On the teardown fallback the count also read the agents' native side (`IsHuman` = a raw pointer read), which `WriteAthleticsSummary` deliberately avoids there. | FIXED in 0558f05 - the header has its own try; on the fallback the count is skipped ("still on the field: not read (…)"). |
| R5 | minor | `Missions/AthleticsLogic.Engine.cs:246` (`Track`), `AthleticsLogic.cs:210` (`OnAgentDeleted`) | An agent index reused before we saw the old agent leave: `Track` only took the old record out of the loop - a running step back or pace hold stayed listed (the tick kept calling the engine on the OLD agent: `DisableScriptedMovement`, `SetScriptedFlags`) and a slowed horse stayed registered. | FIXED in 0558f05 - the same `Forget()` as a man leaving the field (no engine call on him); `OnAgentDeleted` is a backstop that forgets an agent deleted without a removal we saw (a no-op after `OnAgentRemoved`, the normal path). Smoke step "stale records". |
| R6 | minor (perf) | `TraxLog.cs:140` (`Write`) | One `File.AppendAllText` (open / append / close) per line on the main thread: measured ~110 µs a line in Documents with a 2 MB log. PLAYTEST asks for `VerboseLogging` on; a 1000-agent battle then passes ~200 lines/s plus a 40-line burst per bucket at the first clash - ~2% of the main thread and a ~40 ms hitch. | FIXED in edff3dc - one handle kept open, `AutoFlush` (every line still reaches the OS at once: nothing lost on a crash), ~7 µs a line; shared for read/write/delete (an editor can read it meanwhile); follows a changed path; dropped on any write error; closed before a trim and released at every mission end and at unload. A trim that finds no line break no longer re-reads the file at every following line. Smoke step "log file". |
| R7 | **For Anton** | `TraxLog.cs:30` (`TrimAtBytes` 2 MB, CLAUDE.md "~2 MB trim") | With `VerboseLogging` on (PLAYTEST line 7) a big battle writes ~30-50 KB/s (≈ 10 hot buckets × 20 lines/s × 150-250 bytes) → the log reaches 2 MB in ~40-70 s and trims to its newest half every ~35 s after that. So by the end of the playtest only the last minute or two survive: the `[summary]` blocks of the earlier battles - "that alone answers most questions" (PLAYTEST) - are gone. | See "For Anton" below - the cap is his rule. Not changed. |
| R8 | minor (perf) | e.g. `AthleticsLogic.Engine.cs:591`, `Models/DamageRandomizer.cs:95` | With `VerboseLogging` on, each verbose line is BUILT before `TraxLog.Verbose` asks the rate limiter, so most are built and then dropped (every blow, hit, recompute). Estimated ~1-5 ms/s of string work plus garbage in a 1000-agent battle; nothing when verbose is off. | DEFERRED - the fix touches ~30 call sites (a "will this pass?" check before building each line); cost small and verbose-only; not worth the churn right before the playtest. |
| R9 | not a bug | `Core/DamageRoll.cs:226` | `ModEnabled` is checked LAST in `Decide`, not first (CLAUDE.md: "check it before anything else"). | NOT A BUG - deliberate (AI_NOTES step 5b): the hit keeps the game's value either way, and deciding it last lets an OFF battle count exactly the hits an ON battle rolls (the ON/OFF comparison). |
| R10 | not a bug | `Hud/TraxHudView.cs`, game `MissionScreen.cs:4163`, `Mission.cs:4605` | Could a HUD view tick after the mission ended and read a cleared agent (`IsActive()` on a zeroed pointer = an access violation no catch can stop)? `MissionScreen.IsMissionTickable` stays true while `MissionEnded`. | NOT A BUG - `EndMissionInternal` calls the listeners' `OnEndMission` first; `MissionScreen.OnEndMission` finalizes AND unregisters every view before behaviours' `OnEndMissionInternal`, before agents are cleared; the mission state pops in the same tick. |
| R11 | not a bug | `AthleticsLogic.Engine.cs:1267` (`SpeedFactorsFor`), `:187` (`Get`) | `AthleticsLogic.Current` still points at the previous (dead) mission while the next one builds its first agents (before its `AfterStart`). | NOT A BUG - every lookup is by index AND reference (`ReferenceEquals(st.Agent, agent)`), so a new agent never matches a dead record and no native call is made. |
| R12 | not a bug | `Missions/AthleticsLogic.Hud.cs:58` | Views are added with `MissionScreen.AddMissionView` from inside the logic's own tick (the mission is iterating its behaviours). | NOT A BUG - `Mission.AddMissionBehavior` appends and the mission ticks behaviours by index from the end; the new view ticks from the next frame. |
| R13 | not a bug | `Mcm/McmBridge.cs`, all static initialisers | Optional assemblies outside method bodies; static constructors / field initialisers that could load MCM. | NOT A BUG - AssemblyGuard: no MCM / Harmony / ButterLib / UIExtenderEx / NavalDLC / CustomBattle type in any type surface (lambda caches included); every static field of the module is MCM-free (`McmBridge` holds the built page as `object`). |
| R14 | not a bug | whole mod | Anything written into the campaign save. | NOT A BUG - no campaign behaviour, no `SyncData`, no saveable type; MCM runs with format "none"; everything lives in mission objects and config.json. |
| R15 | not a bug | `ConfigStore.cs:50-51` | Static event subscriptions outliving a mission (leaks, calls into a dead mission). | NOT A BUG - the only `+=` in the mod: `TraxSettings.Changed` / `HandlerFailed` by `ConfigStore`, once, for the app's lifetime; missions POLL `Settings.Version`; no Mission / MCM event is subscribed per mission (MCM's page callback is a static method). |
| R16 | not a bug | `Missions/PaceBody.cs` | The pace hold's NoAttack shares the flag word with vanilla (item pickup, `UseGameObject`, standing points, ladders): could we lift the game's NoAttack, or leave ours on? | NOT A BUG - Start refuses a man with GoToPosition / NoAttack / an object / a ladder / a detachment / a horse; Release waits while an object / ladder / walk to an object is on him (≤ 3 s under a plain scripted frame); a flag already cleared counts "cleared by the game"; no vanilla system READS NoAttack as a signal (grep of the decompile). |
| R17 | not a bug | tick, hit hooks, HUD frames | 1000-agent cost: per-tick allocations, O(N²) loops, `UpdateAgentProperties` churn, widget walks per frame. | NOT A BUG - no allocation on any per-tick / per-hit / per-frame path with verbose off (no LINQ, closures, boxing, non-struct enumerators; rules and frames are structs); the only list searches are `_stepping` (≤ `StepBackMaxAtOnce`) and `_paceHeld` (held men) at an END; recomputes move in 0.05 steps under a 50-a-tick budget (~6 a tick estimated in a 1000-man melee); the strip walks the card tree once per menu open and ≤ 16 card parents per frame. |
| R18 | minor | `AthleticsLogic.Engine.cs:166` (`SweepAgents`) | One try around the whole first-tick sweep: an exception on one agent left everyone after him untracked for the battle (no cost, no penalty, no bar). | FIXED in 0da7b9a - one try per agent. |
| R19 | minor | `AthleticsLogic.Engine.cs:160`, `AthleticsLogic.cs:129` | `AthleticsLogic.Current` is one static slot. If a mission ever starts on top of another and ends, its teardown empties the slot while the older mission resumes: its penalties and HUD would stop. (No such stacking seen in 1.4.8 SP - defensive.) | FIXED in 0da7b9a - each tick takes the slot back when this mission is `Mission.Current`. |
| R20 | not a bug | `ConfigStore.cs:225` | Dragging an MCM slider logs one `[config]` line per step, unthrottled. | NOT A BUG - "every change is logged" is a hard requirement; a drag is a few dozen lines at ~7 µs each now (R6). |
| R21 | minor | `SettingsSchema.cs` (`ShowTargetBar`, `TargetBar*`, `ShowFormationBars`, `FormationBarsAlways`, `FormationBarHeight`) | Settings of the LATER features (steps 7, 8) are in MCM and the file but do nothing. | DEFERRED - exactly step 10b's first item ("hide the LATER features' settings"). |
| R22 | not a bug | `module/GUI/Prefabs/*.xml`, `Hud/HudLayer.cs` | Could our full-screen HUD layers eat mouse clicks (the orders strip sits over vanilla's clickable cards)? | NOT A BUG - every widget of both prefabs has `DoNotAcceptEvents` (the roots also `DoNotPassEventsToChildren`); our layers use local order 1, vanilla's order layer 14. |
| R23 | not a bug | step back, pace hold, penalties, layers | The stuck-state audit: is every scripted step, NoAttack, speed / AI-value penalty and HUD layer released on every path? | NOT A BUG (after R1-R5) - step back: time, order / arrangement / formation change, detach, player, mount, rout, switch-off (any of 3 switches, at once), left the field, stale / deleted record, mission end (through the engine before the summary); pace hold: time, a swing slipping through, switch-off, left the field, player, mount, mission end, a game job waited out; penalties: every off-switch refills and re-targets everyone (≤ 50 recomputes a tick) and the decorator itself returns vanilla the moment ModEnabled / AthleticsEnabled is off; HUD layers: gate every frame, disabled on error, finalized with the screen. |
| R24 | not a bug | whole mod | Hot swap: every parameter read at use time or re-applied on change. | NOT A BUG - rules are rebuilt from `TraxSettings.Shared` per tick / hit / refresh; the `_seen*` fields only detect changes; speeds re-targeted, AI values re-applied, strip layout re-applied on the settings version; `StepBackHoldAttacks` applies to the next step back (a running one ends within `StepBackSeconds`). |

## For Anton

**R7 - the log trims away earlier battles when VerboseLogging is on.** The log is capped at about
2 MB (your "one rolling log file" rule; it keeps the newest half). With verbose logging on, a big
battle writes it that fast that only the last minute or two survive, so by the time you hand me
the log, the `[summary]` of every earlier battle is gone - and the summaries are what I read
first. Nothing is broken with verbose OFF (a battle is a few hundred lines). Pick one:

1. **Keep every `[summary]`, `[error]` and `[mission]` line through a trim** (they are ~60 lines a
   battle) - still one file, still ~2 MB, the chatty lines roll off. My recommendation.
2. **Raise the cap** to about 50 MB for the playtest (one line of code).
3. **Leave it** and play with `VerboseLogging` off except in one battle you want to look at closely
   (then hand me the log right after that battle).

Until you choose, PLAYTEST's "turn on VerboseLogging for the first run" would cost the summaries -
step 10b (the PLAYTEST rewrite) should follow whatever you pick.

## Checked and fine (no finding)

- Every engine callback, tick, model override, VM / view method and MCM callback is wrapped;
  every catch logs `[error]` with its stack (first per site per mission, the rest counted) and
  falls back to vanilla. Base calls stay outside our try where they are the game's own code.
- Null / removed agents, riders without mounts, riderless horses, missiles with no shooter and
  siege-engine shots: every path null-checks and reference-checks; siege engines never reach
  `OnAgentShootMissile`; a horse charge's upside follows the rider.
- Mission kinds: field battle, siege, sally-out, hideout (stealth, then battle; the boss duel is
  not battle mode), naval (step back off), tournaments and arena (step back off by behaviour
  name), custom battle (general / heroes as leaders), loading a save or quitting to the menu
  mid-battle (`MBGameManager.EndGame` ends the mission first, so the summary is written).
- DESIGN §1-§4 rules against the code: the roll and its skips, the pool / floor / f / health
  cap, costs (10 / 7.5 / 5.6) and the custom-battle general, regen by effort, the three curves,
  the three attack-rate techniques, the step-back chance and safety rules, the bar colours and
  marker, the orders strip, the master switch.
- Logging: rate limits per bucket (verbose 40 + 20/s, notices 30 + 1/s, errors 3 + 1 per 10 s),
  suppressed counts written at every summary, a summary on every mission-end path.

# Design — Trax Combat Enhancements

The contract for what the mod does. Anton's ask (2026-09-27) is the source; where it was
ambiguous, the reading chosen is written down under **Interpretations** so it can be
overturned in one line. Every number here is a parameter (see **Parameters**) — nothing is
hard-coded.

Target: Mount & Blade II: Bannerlord **v1.4.8** (singleplayer). Released on **Steam Workshop only**.

---

## 1. Damage randomness

Every strike that LANDS — melee or ranged, by anyone on any side, player included — has its
final damage multiplied by a fresh random factor drawn uniformly from
`[1 − p, 1 + p]`, `p = DamageRandomPercent / 100` (default 50%).
A 50-damage hit lands for anything from 25 to 75, rolled anew on every hit.

- Applies to hits on people and (toggle) on horses. Horse-charge bumps count as melee.
- NOT randomized: blows stopped by a shield (toggle `DamageRandomOnShields`, off), fall
  damage (not a strike), hits on doors, siege engines and other objects.
- A hit the game computes as 0 stays 0. A positive hit never rounds below 1.
- Knockdown, stagger and dismount stay the game's own rules — but they read the final
  (rolled) damage, so a high roll knocks down more often than a low one. Crush-through is
  decided before damage and is unaffected.

## 2. Endurance

A per-fighter pool in every combat mission. Simpler cousin of RCM's posture: it only ever
drains by attacking, and the only penalty is slow attacks when empty.

- **Pool**: `MaxEndurance` (100). Every fighter starts a mission full.
- **Cost**: every blow costs `CostPerBlow` (10) × multipliers:
  - heroes (lords, companions, the player): × `HeroCostMultiplier` (0.75)
  - the hero who LEADS the fighter's party (the player for their own party, a lord for his):
    × `PartyLeaderCostMultiplier` (0.75) on top → 0.75 × 0.75 × 10 = 5.6 per blow.
  - This is what makes the game hero-centred: the player's party leader lasts ~18 blows,
    a common soldier 10.
- **What is a blow**: a melee swing or thrust, a shot, a throw. A couched lance or braced
  spear hit has no swing, so it costs one blow when it LANDS. Kicks, shield bashes and
  siege engines (ballista, onager) cost nothing.
- **Exhausted**: at 0 endurance the fighter attacks at `ExhaustedAttackSpeedPercent` (20%)
  of normal speed — melee swings and thrusts, bow draw, crossbow reload, throws. Speed comes
  back once endurance rises above `ExhaustedRecoverPercent` of the pool (0 = the moment it
  is above 0, as asked).
- **Changing `MaxEndurance` mid-battle** keeps each fighter's fraction (60% stays 60%).
- **Regeneration**: starts after `RegenDelayBlowTimes` (2) × `BlowTimeSeconds` (1.5 s) with
  no attack. Then refills from 0 to full in:
  - `FullRegenSecondsStanding` (60 s) while standing still,
  - `FullRegenSecondsMoving` (120 s) while moving (walking, running, or riding at speed above
    `MovingSpeedThreshold`).
  Any new blow stops regeneration and restarts the delay.
- **Cavalry**: riders use the very same pool. Riding never drains it; only blows do. The
  horse has no endurance of its own.

## 2b. Endurance v2 — Anton's additions (2026-09-27), lands in steps 5b–5d

Step 5 builds §2 as written; step 5c then reshapes it as below and folds this section into
§2. Where the two disagree, this section wins.

- **The pool follows Athletics** (Anton: "I was always thinking of the Athletics skill when
  speaking about endurance"). Pool = `EnduranceBase` (50) + `EndurancePerAthletics` (0.5)
  × the fighter's Athletics skill: Athletics 0 → 50, 100 → 100, 200 → 150, 300 → 200.
  Riders too — Athletics, never Riding. Replaces the flat `MaxEndurance`. Changing either
  number mid-battle keeps each fighter's fraction.
- **The peak line** `EndurancePeakPoints` (100, in POINTS, not a percent of the pool). At or
  above it a fighter is at their peak: full damage upside, full swing speed, full run
  speed, never steps back. Below it, each of those falls in a straight line with the points
  left, down to its floor at 0. A 150-pool veteran stays at peak from 150 down to 100; a
  50-pool recruit never reaches it.
- **Health caps the pool** (`HealthCapsEndurance`, on): the usable pool = pool × health
  left. Pool 100 at 75% health → 75; a fighter holding 80 drops to 75 at once, and regen
  never fills above the cap.
- **Damage upside follows endurance** (`DamageBonusFollowsEndurance`, on): the roll becomes
  `[1 − p, 1 + p × min(E / peak, 1)]` with E = the attacker's endurance (the rider's for a
  horse charge). At or above the peak +50%; at 50 points +25%; at 0 no upside at all — only
  the −50% side. The downside never changes.
- **Swing speed is gradual** (Anton's pick, replaces the cliff): attack speed =
  S + (1 − S) × min(E / peak, 1), S = `ExhaustedAttackSpeedPercent` (20%). Full speed at
  the peak, 20% at 0. `ExhaustedRecoverPercent` retires. "Exhausted" still means E = 0 (for
  logs and bars).
- **Run speed follows endurance**: top speed on foot = M + (1 − M) × min(E / peak, 1),
  M = `MinMoveSpeedMultiplier` (0.3). Tired men slow down, so fresher men overtake them.
  Horses keep their speed (Anton's pick): `MountMinSpeedMultiplier` (1.0 = unaffected;
  lower it to let a tired rider's horse slow on the same curve).
- **Regen follows effort** (replaces standing/moving): standing or walking refills the pool
  in `FullRegenSecondsStanding` (60 s). Faster than a walk, the rate falls in a straight
  line to × `RegenMultiplierAtFullRun` (0.5) at the fighter's top speed. Effort = speed ÷
  current top speed (the horse's for riders); walking = effort up to `WalkEffortFraction`
  (0.4 — step 5c checks the game's real walk/run ratio and sets it). `FullRegenSecondsMoving`
  and `MovingSpeedThreshold` retire.
- **Tired fighters step back** (step 5d, `StepBackEnabled`, on): after each MELEE swing an
  AI fighter on foot may step back, facing its enemy with its guard up. Chance =
  `StepBackMaxChancePercent` (100) × (1 − min(E / peak, 1)): 0% at the peak, 50% at half,
  every swing at 0. Back `StepBackDistance` (2 m) for up to `StepBackSeconds` (1.5 s), then
  the formation takes it again. Never the player, never riders, never after ranged attacks.
  The point: the tired fall back and the fresh step in. If the game's AI fights this,
  step 5d reports and proposes the nearest thing that works.

## 2c. Defaults file (step 5b)

`defaults.json` at the repo root holds EVERY parameter's default, one per line, with the
plain-words explanation above it — the one place Anton tunes defaults, then pushes. It is
the single source of defaults: the build embeds and ships it; the first-run config file,
MCM's Default preset and a **"Revert all to defaults"** button in MCM all read it. A second
MCM button writes the CURRENT values as a defaults file in the config folder, so a good
tuning found in game can be copied into the repo. The Default column of the Parameters
table below is the INITIAL value; `defaults.json` wins.

## 3. Showing endurance

All toggles, all on by default.

1. **Player bar** (`ShowPlayerBar`): the player's endurance near the vanilla health bar,
   in the spirit of RBM's posture bar (RBM = Realistic Battle Mod, confirmed by Anton —
   style reference only).
2. **Looked-at NPC** (`ShowTargetBar`): a small bar for the fighter the player is aiming at
   / looking at, within `TargetBarMaxDistance`; it lingers `TargetBarLingerSeconds` after
   the aim leaves so it does not flicker. Aiming at a horse shows its rider.
3. **Squad bars** (`ShowFormationBars`): above each of the PLAYER'S formations, a bar of the
   formation's average endurance, with a band of ± `FormationSpreadStdDevs` (1) standard
   deviations (`ShowFormationSpread`). `FormationBarsAlways` off = only while vanilla shows
   its formation markers (marker key held or orders menu open).
4. **Orders menu** (`ShowInOrderMenu`): while the orders menu is open, a compact panel of our
   own lists each formation's average ± spread ("Infantry 72 ± 8") in the cards' order.
   Numbers INSIDE vanilla's cards would need UIExtenderEx and risk clashing with RTS Camera
   Command System — not worth it (Claude's call 2026-09-27, see RESEARCH implication 1).

Bars show only in fights (battle, duel, tournament modes) and never while the game's
"hide battle UI" is on.

**Additions (Anton, 2026-09-27):**
- The player and target bars show the NUMBER and a marker at the peak line (100 points).
  Fill colour by points: above the peak green; at or below the peak blue; at or below
  `BarYellowBelowPercent` (75) % of the peak yellow; `BarOrangeBelowPercent` (50) orange;
  `BarRedBelowPercent` (25) red.
- Squads also show their AVERAGE HEALTH (`ShowFormationHealth`, on) — above the formation
  and in the orders menu.
- Orders menu: our strip sits directly UNDER the vanilla formation cards, one cell per card
  ("below the arrows remaining", Anton's words) — endurance average ± spread and average
  health. Still no UIExtenderEx; if the cards' positions cannot be matched reliably, the
  step falls back to a compact panel and says so.

## 4. Configuration

Every parameter lives in two places that stay in sync:

- a config file created on first run under
  `Documents\Mount and Blade II Bannerlord\Configs\TraxCombatEnhancements\` — hand-editable,
  with a plain-words explanation beside every value;
- the **Mod Configuration Menu (MCM)** when installed. MCM must stay OPTIONAL: without it the
  mod runs on the file alone (see CLAUDE.md, hard requirements).

**Hot swap**: a change in MCM applies LIVE — the next hit, blow or HUD refresh uses it, even
mid-battle. Nothing ever needs a game restart; if some parameter can only apply from the
next battle, its description says so. Hand edits to the file are picked up at the next
battle start.

**When MCM writes the file** (its Done button), a hand edit made meanwhile is never lost: the
file on disk is re-read at that moment, MCM's value is written only for the settings MCM
changed, and every other setting keeps the value on disk (it takes effect at the next battle
start). Unknown keys are kept in a "not recognised" section; a file that does not parse is
saved as `config.json.broken-<time>` before a fresh one replaces it. MCM's Reset buttons
restore the defaults of the table below. Ranges live in the schema
(`src/TraxCombat.Core/SettingsSchema.cs`) and are printed beside each key in the file. The
file also carries `ConfigVersion`, a format stamp — not a setting.

## 5. Compatibility

- **RBM (Realistic Battle Mod) is NOT compatible** (Anton, 2026-09-27): it has its own
  posture and stamina and patches the same combat. The Steam page says so plainly. If RBM
  is enabled, the mod logs `[compat]` and shows ONE message at the main menu / campaign
  start ("Trax Combat Enhancements is not compatible with Realistic Battle Mod — disable
  one of them"). It does not refuse to run.
- Other combat mods that change damage or attack speed may stack with ours; the log's
  per-battle summary is the tool to see it.

## Parameters

| Key | Default | What it does |
|---|---|---|
| `DamageRandomEnabled` | true | Master switch for damage randomness. |
| `DamageRandomPercent` | 50 | ± spread in percent. 50 → a 50-damage hit lands for 25–75. 0 = off. |
| `DamageRandomMelee` | true | Randomize melee hits. |
| `DamageRandomRanged` | true | Randomize arrows, bolts and thrown weapons. |
| `DamageRandomOnMounts` | true | Randomize hits that land on horses too. |
| `DamageRandomOnShields` | false | Randomize the damage a shield takes when it blocks. |
| `EnduranceEnabled` | true | Master switch for endurance. |
| `MaxEndurance` | 100 | Size of the pool. |
| `CostPerBlow` | 10 | Endurance one blow costs before multipliers. |
| `CostOnMiss` | true | true: every attack costs, landed or not. false: only blows that land. |
| `HeroCostMultiplier` | 0.75 | Cost multiplier for heroes. |
| `PartyLeaderCostMultiplier` | 0.75 | Extra multiplier for a party's leading hero, on top of the hero one. |
| `ExhaustedAttackSpeedPercent` | 20 | Attack speed at 0 endurance, percent of normal. |
| `ExhaustedRecoverPercent` | 0 | Once exhausted, speed returns only above this % of the pool. 0 = as soon as it is above 0. |
| `RegenDelayBlowTimes` | 2 | Idle blows before regeneration starts. |
| `BlowTimeSeconds` | 1.5 | How long "one blow" is, for the delay above. |
| `FullRegenSecondsStanding` | 60 | Seconds from empty to full while standing still. |
| `FullRegenSecondsMoving` | 120 | Seconds from empty to full while moving or riding. |
| `MovingSpeedThreshold` | 0.5 | Speed (m/s) above which a fighter counts as moving. |
| `ShowPlayerBar` | true | Player endurance bar. |
| `ShowTargetBar` | true | Bar for the fighter you look at. |
| `TargetBarMaxDistance` | 30 | Metres — how far away a looked-at fighter still gets a bar. |
| `TargetBarLingerSeconds` | 2 | Seconds the bar stays after your aim leaves the fighter. |
| `ShowFormationBars` | true | Average bars above your formations. |
| `FormationBarsAlways` | true | true: always shown. false: only while vanilla shows formation markers. |
| `ShowFormationSpread` | true | ± spread band on the formation bars. |
| `FormationSpreadStdDevs` | 1.0 | Band width in standard deviations. |
| `FormationBarHeight` | 3.0 | Metres above the formation's centre for its bar. |
| `ShowInOrderMenu` | true | Panel of formation averages ± spread while the orders menu is open. |
| `HudRefreshSeconds` | 0.1 | (Advanced) How often the player and target bars update. |
| `FormationStatsRefreshSeconds` | 0.25 | (Advanced) How often formation averages and spreads are recomputed. |
| `VerboseLogging` | false | Log every roll, blow and exhaustion (rate-limited) to `trax_combat.log`. Off = load, settings, mission start/end, per-battle summaries and errors only. |

New parameters discovered while building go into this table in the same commit.

## Planned parameters (§2b, §3 additions — not in the schema yet)

The step that builds each one moves its row into the Parameters table above (the schema
test reads that table only) and removes the retired rows in the same commit.

| Key | Default | Step | What it does |
|---|---|---|---|
| `EnduranceBase` | 50 | 5c | Pool at Athletics 0. Replaces `MaxEndurance`. |
| `EndurancePerAthletics` | 0.5 | 5c | Pool added per Athletics point (300 → +150). |
| `EndurancePeakPoints` | 100 | 5c | Points at or above which a fighter is at their peak. |
| `HealthCapsEndurance` | true | 5c | Health left caps the usable pool. |
| `DamageBonusFollowsEndurance` | true | 5c | The damage upside shrinks with the attacker's endurance below the peak. |
| `MinMoveSpeedMultiplier` | 0.3 | 5c | Top speed on foot at 0 endurance. |
| `MountMinSpeedMultiplier` | 1.0 | 5c | Horse top speed at the rider's 0 endurance (1.0 = horses never slow). |
| `RegenMultiplierAtFullRun` | 0.5 | 5c | Regen rate at top speed, relative to standing or walking. |
| `WalkEffortFraction` | 0.4 | 5c | Up to this share of top speed counts as walking (full regen). |
| `StepBackEnabled` | true | 5d | Tired AI fighters on foot step back after melee swings. |
| `StepBackMaxChancePercent` | 100 | 5d | Chance to step back at 0 endurance (0 at the peak, straight line between). |
| `StepBackDistance` | 2 | 5d | Metres a fighter steps back. |
| `StepBackSeconds` | 1.5 | 5d | Longest a step back lasts before the formation takes over again. |
| `BarYellowBelowPercent` | 75 | 6 | Bar turns yellow at or below this % of the peak. |
| `BarOrangeBelowPercent` | 50 | 6 | Orange at or below this %. |
| `BarRedBelowPercent` | 25 | 6 | Red at or below this %. |
| `ShowFormationHealth` | true | 8 | Squad bars and the orders-menu strip also show average health. |

Retire in 5c: `MaxEndurance`, `FullRegenSecondsMoving`, `MovingSpeedThreshold`,
`ExhaustedRecoverPercent`.

## Interpretations (Anton can overturn any of these)

1. **"A blow" = an attack the fighter makes** — swing, thrust, shot, throw — landed or not.
   Anton's damage rule says "each time it lands"; the endurance rule does not, so every
   attack costs. `CostOnMiss = false` switches to landed-only.
2. **Hero multiplier is 0.75.** The ask said 0.75 and also "0.5, 10 → 5"; the leader
   formula 0.75 × 0.75 × 10 settles it at 0.75. One number to change if 0.5 was meant.
3. **"Party leader" = the hero leading the fighter's own party** — the player, and AI lords
   for their own parties. Stacks with the hero multiplier.
4. **Blocking, running and riding cost nothing.** Only blows drain. (Blocking: confirmed by
   Anton, 2026-09-27.)
5. **Exhaustion is a cliff at 0**, as asked — no gradual slowdown.
6. **Applies in every combat mission** (field battles, sieges, hideouts, custom battles,
   tournaments and arena). Per-mission toggles only if playtest asks for them.
7. **Custom battle has no parties**: there the "party leader" is the side's general, or —
   when a side has none — every hero on that side.
8. **Kicks and shield bashes are free**; a couched-lance / braced-spear hit costs one blow
   when it lands; siege engines are free.
9. **Shield damage is not randomized** by default; horse-charge bumps follow the melee
   toggle; fall damage and hits on objects never roll.
10. **RBM is declared incompatible** (Anton confirmed, 2026-09-27) — see §5.

Decisions 7–10 and the new parameters `DamageRandomOnShields`, `ExhaustedRecoverPercent`,
`TargetBarMaxDistance`, `TargetBarLingerSeconds`, `FormationBarsAlways`,
`HudRefreshSeconds`, `FormationStatsRefreshSeconds` came out of step 2's research
(`docs/RESEARCH.md`, "Design implications"), settled by Claude while Anton was away
(2026-09-27) — every one is a default he can overturn.

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

## 3. Showing endurance

All toggles, all on by default.

1. **Player bar** (`ShowPlayerBar`): the player's endurance near the vanilla health bar,
   in the spirit of RBM's posture bar (Anton said "RCM"; the installed mod with the posture
   bar is RBM, Realistic Battle Mod — style reference only).
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
10. **Play with RBM disabled** — it has its own posture and stamina; two stamina systems at
    once would be confusing and untestable.

Decisions 7–10 and the new parameters `DamageRandomOnShields`, `ExhaustedRecoverPercent`,
`TargetBarMaxDistance`, `TargetBarLingerSeconds`, `FormationBarsAlways`,
`HudRefreshSeconds`, `FormationStatsRefreshSeconds` came out of step 2's research
(`docs/RESEARCH.md`, "Design implications"), settled by Claude while Anton was away
(2026-09-27) — every one is a default he can overturn.

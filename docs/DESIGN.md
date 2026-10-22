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
- A tired attacker loses the lucky side of the roll (§2, `DamageBonusFollowsAthletics`): his
  range is `[1 − p, 1 + p × f]`, f = his share of the peak line left.

### Overall damage reduction (step 25)

Anton (2026-10-10): "add 25% overall damage reduction on all sources (configurable), so if I do 100 dmg with a
swing on average -> 75 dmg. This will further slow down the combat."

- Every hit that lands on a person or a horse — melee, arrows, bolts, thrown weapons, siege-engine shots, kicks,
  shield bashes, horse-charge bumps, friendly fire — deals `DamageReductionPercent` (25) % less, AFTER the roll:
  the game's models (armor included) → the ±p roll → × (1 − r/100) → the game rounds. A 100 hit rolls 50–150 and
  lands 38–113, 75 on average.
- Shield blocks are reduced too (the shield wears down a quarter slower), whatever `DamageRandomOnShields` says.
- It does NOT follow the roll's switches: with `DamageRandomEnabled` off, or a hit the roll's toggles skip, the
  game's value is still reduced. Only the master switch turns it off (vanilla damage) — and 0.
- NOT reduced: falls (not a blow), hits on doors, siege engines, ships and other objects (sieges keep the game's
  pace). A hit the game computes as 0 stays 0 (blocked blows that deal nothing stay nothing). A positive hit never
  lands below 1. The top of the range is 90 %: nothing is ever made unkillable.
- Knockdown, stagger and dismount read the final damage (§1), so they come a little less often, in step with it.

## 2. Athletics (the stamina bar)

A per-fighter pool — the **Athletics** bar — in every combat mission. Simpler cousin of RCM's
posture: it drains by attacking (and, since step 22, a little by blocking). Near the top of his OWN bar a fighter is at full
strength; below that line his damage upside, attack rate and run speed fall in straight lines,
down to long pauses between attacks and a slow run when it is empty. (Athletics v2, Anton 2026-09-27, built in
step 5c: it replaced step 5's flat 100-point pool, the cliff at 0 and the standing/moving
regen.)

- **It is called ATHLETICS, everywhere** (Anton, 2026-09-27: "so it is the athletics bar that
  gets depleted" — so every player sees at once that the Athletics skill controls all of
  it). The pool, the bar, the points: **Athletics**. The skill on the character screen:
  **Athletics skill**. Every player-facing word — MCM, config keys and comments, bars,
  messages, log tags (`[athletics]`), README, Steam page — and the code too (one
  vocabulary: `AthleticsLogic`, `AthleticsEnabled`, `FormationAthleticsStats` and so on).
  The word used to be "endurance" (renamed in step 5b); TASKS_DONE history and RESEARCH's
  findings keep their words.
- **The pool IS the Athletics skill** (Anton's pick): pool = max(`AthleticsPoolFloor` (50),
  `AthleticsPoolPerSkill` (1.0) × Athletics skill), never below 1 point. Skill 180 → the bar
  tops at 180; a 300-skill hero gets 300. The bar's number matches the skill screen: heroes
  (lords, companions, the player) use their real skill, troops the skill in their troop data.
  Riders too — Athletics, never Riding. Every fighter starts a mission full. Changing either
  number mid-battle keeps each fighter's fraction (60% stays 60%). The floor is Anton's
  experiment slider: real troops (v1.4.8 data) have Athletics 20 (recruits), 40 (tier 2),
  60 (elite cataphract), 130 (legionary), 170 (Fian champion) — without the floor a recruit
  would be empty after two swings.
- **Cost, in points**: every blow costs `CostPerBlow` (10) POINTS × multipliers, whatever the
  pool — so a bigger Athletics pool = more blows = heroes stronger:
  - heroes (lords, companions, the player): × `HeroCostMultiplier` (0.75)
  - the hero who LEADS the fighter's party (the player for their own party, a lord for his):
    × `PartyLeaderCostMultiplier` (0.75) on top → 0.75 × 0.75 × 10 = 5.6 per blow.
  - Step 43 (Anton, 2026-10-17): defaults.json ships `CostPerBlow` **3** (3 / 2.25 / 1.69) - the AI's eagerness now
    follows its bar (§2 "Aggression and the fever"), so a swing may cost less; the worked examples in this section keep the INITIAL 10 (the unit
    tests count blows on them). Config format 8 moves a file still holding 10 to 3 once (and `CostPerWrongSideShieldBlock`
    5 → 2); a tuned value is kept.
  - Step 46 (Anton's playtest, 2026-10-17): defaults.json ships `CostPerBlow` **7** (7 / 5.25 / 3.94). Config format 9 moves a
    format-8 file still holding 3 to 7 once (a tuned value is kept and noted); an older file's 10 goes straight to 7.
  - Step 68 (Toni's 100 v 100, 2026-10-19: "tire the men more to slow down the combat again a bit after the first impact";
    decision 61): defaults.json ships `CostPerBlow` **8** (8 / 6 / 4.5 - a recruit's 50-point bar empty after 7 blows, a
    legionary's 130 after 17). Config format 14 moves a file still holding 7 once (a tuned value is kept and noted); an older
    file's 10 goes straight to 8.
  - Step 71 (Toni, 2026-10-19: "bump up how fast they tire, back to 10 for the swings ... they need to tire faster still";
    decision 65): defaults.json ships `CostPerBlow` **10** again (10 / 7.5 / 5.63 - a recruit's 50-point bar empty after 5
    blows, a legionary's 130 after 13). Config format 15 moves a file still holding 8 once (a tuned value is kept and noted); an
    older file's 7 goes straight to 10. The refill is unchanged.
- **What is a blow**: a melee swing or thrust, a shot, a throw. A couched lance or braced
  spear hit has no swing, so it costs one blow when it LANDS. Siege engines (ballista, onager)
  cost nothing.
- **Kicks and shield bashes cost too — less** (Anton, 2026-09-28; built in step 18): each costs
  `CostPerKickOrBash` (3) POINTS × the same hero and party-leader multipliers as a blow — 3 for a
  soldier, 2.25 for a hero, about 1.7 (1.69) for a party leader; 0 = free, as they were until step 18.
  Step 66 (Anton: "a bit more taking of the energy, say 5 pts each"): 5 in defaults.json - 5 / 3.75 / 2.81 (decision 59).
  Step 71 (Toni: "they need to tire faster still"): 6 - 6 / 4.5 / 3.38 (decision 65; config format 15 moves a file's 5 once).
  Charged ONCE per kick or bash, the moment it starts, landed or not (`CostOnMiss` is for blows);
  the AI's and yours alike. Otherwise it is paid like a blow: the curves follow, it can take a
  fighter below his peak line or empty him, and it restarts the refill delay. It is **not a blow**:
  it never starts the no-attack pause below (step 13's rules stand - a kick is never held, a bash
  waits while a pause runs), never rolls a step back, and the log counts it apart. Riders cannot
  kick; a mounted bash, should the engine ever play one, is charged the same.
- **Defending costs too** (Anton, 2026-09-29, for slower battles: "defending with shield in the right
  direction 1, with shield - wrong direction 5, without shield - 2"; built in step 22). Every melee blow a
  fighter BLOCKS costs HIM, the defender: `CostPerShieldBlock` (1) POINTS when his shield stops it on the
  correct side, `CostPerWrongSideShieldBlock` (5) when the shield stops it held to the wrong side (the game
  judges the side and weakens such a block itself), `CostPerWeaponParry` (2) when he blocks or parries it
  with a weapon, no shield (a chamber block too) — each × the same hero and party-leader multipliers as a
  blow (1 / 5 / 2 for a soldier, 0.75 / 3.75 / 1.5 for a hero, 0.56 / 2.81 / 1.13 for a party leader); 0 =
  free. (defaults.json: step 43 shipped the wrong side at 2; step 68 - Toni, decision 61 - ships the right side **2** and
  the wrong side **5** again, the parry 2: a soldier 2 / 5 / 2, a hero 1.5 / 3.75 / 1.5, a hero party leader 1.13 / 2.81 /
  1.13; config format 14 moves a file's 1 / 2 once; step 71 - Toni, decision 65 - ships the right side **3**, the wrong side **6**
  and the parry **3**: a soldier 3 / 6 / 3, a hero 2.25 / 4.5 / 2.25, a hero party leader 1.69 / 3.38 / 1.69; config format 15
  moves a file's 2 / 5 / 2 once.) The AI's and yours alike, on foot or mounted. Charged ONCE per blocked blow (a swing touching the
  guard twice is one blow). Paid like a kick: the curves follow, it can take a man below his peak line or
  empty him. **The refill delay** (step 29, Anton 2026-10-12, after a tournament 1v1 where both men sat at
  empty for good: "a skilful defender should recover while staying on the defence"): with
  `OnlyWrongSideBlocksStopRefill` on (the default) ONLY a wrong-side shield block restarts the refill delay
  and ends a refill run; a right-side shield block and a weapon parry still cost their points, but the
  delay clock and a running refill are left alone - the bar dips by the cost and keeps climbing underneath
  (net up or down by the rates: see AI_NOTES "Step 29" for the numbers). Off = step 22: every paid block
  restarts the delay — a man under attack is not resting. His own attacks, kicks and bashes restart it
  either way. It is not an
  attack: it never starts an attack pause, never rolls a step back, never changes a damage roll, and
  blocking itself is never held or slowed. **Free**: missiles stopped by a shield (for now - counted in the
  summary), a blocked kick or shield bash, a blow the shield on his BACK stopped (he did not defend), and
  everything while the master switch or `AthleticsEnabled` is off. The attacker's blow is charged as
  before - a blocked swing is still a swing.
- **The peak zone** — the top of every fighter's OWN bar (Anton, 2026-09-27). At or above
  `AthleticsPeakPercent` (75) % of the fighter's pool the bar is GREEN and the fighter is at
  full strength: full damage upside, full attack rate (no tired pause - step 21's battle pace below still slows
  the AI's foot melee and archers a little, fresh or not), full run speed, never steps back.
  Below the line each of those falls in a straight line down to its floor at 0. With cost in
  points, bigger pools stay in the zone longer — recruit (floor 50): 2 swings at full
  strength, 5 to empty; legionary (130): 4 and 13; Fian champion (170): 5 and 17; a
  300-skill party leader (cost 5.6): 14 and 54 (53⅓ blows' worth). A fresh fighter always
  runs in and lands its first blows at full strength.
  `f = min(E / (AthleticsPeakPercent% × pool), 1)` — the share of the peak line left, E =
  current Athletics points — drives every curve below.
- **Health caps the pool** (`HealthCapsAthletics`, on): the usable pool = pool × health
  left. Pool 100 at 75% health → 75; a fighter holding 80 drops to 75 at once (at the hit),
  and regen never fills above the cap. The peak line stays measured on the FULL pool, so a
  badly wounded fighter can never climb back into full strength (at 50% health, f is at most
  0.67) — wounds make you weaker, not only shorter-winded.
- **Below the peak, three things weaken** (each 1 at f = 1, its floor at f = 0):
  - **Damage upside** (`DamageBonusFollowsAthletics`, on): the roll of §1 becomes
    `[1 − p, 1 + p × f]` with f from the ATTACKER (the rider's for a horse charge; an attacker
    Athletics does not follow keeps the full upside). In the peak zone +50%; halfway down to
    0, +25%; at 0 no upside at all — only the −50% side. The downside never changes.
  - **Attack speed** = m = S + (1 − S) × f, S = `ExhaustedAttackSpeedPercent` (20%): full in the
    peak zone, 20% at 0 — a straight line, no cliff. **Exhausted** means E = 0 (for logs and
    bars). **What "attack speed" means (Anton, 2026-09-27): the RATE of attacking** — at m 0.5 a
    fighter who attacked once a second attacks once every two seconds; the combat slows down,
    not the swing.
    **PAUSE ONLY (Anton's playtest call, 2026-09-27; built in step 13 — research and every
    decision in AI_NOTES "Step 13").** The animation slow-down of steps 5-12 read as "slow-mo"
    and felt strange, so every attack animation — wind-up, swing, thrust, bow draw, throw,
    reload — now plays at FULL speed (step 20b: a little slower when tired, ×0.85 at empty by
    default - see "The animations" below; the pause is unchanged), and the slow-down is a **no-attack timer** after
    each attack: an attack of duration **D** that ends at attack speed **m** leaves a pause of
    **D × (1/m − 1)** in which the fighter may not START another attack — so the attacking part
    of his rhythm runs at × m (m 0.5: pause = D; m 0.2 at empty: pause = 4 × D). At full
    strength (m 1) there is no tired pause (step 21: the AI's battle-pace share may still apply - below); a pause
    under 0.1 s is not started (plumbing).
    - **D** = the attack's own time: its wind-up (the ready up to full — the part a blow is HELD
      ready is not counted) + its release (the swing with its follow-through). Ranged: the draw
      + the loose + **the reload that follows** (nocking, winding a crossbow, taking the next
      javelin); the pause starts when that reload ends. Melee: at the swing's end (a block
      recoil plays inside the pause). m is the exact curve value when the attack ends.
    - **Never held**: blocking and parrying, moving, switching weapons, kicks (a key and an
      action of their own; since step 18 a kick costs Athletics but never starts or waits out a
      pause). Shield bashes — the attack button while blocking — wait with every other use of the
      attack button (since step 18 they cost Athletics too, but never START a pause). The reload
      itself is never held; the next draw, aim or throw waits.
    - **You** (`AttackRatePlayerTimer`, on): below the peak line your attack button does nothing
      while your pause runs — no wind-up at all, no stuck state; **keep it held and your next
      attack starts the moment the pause ends**. The hold begins at your release's start when
      the pause it will leave is sure to be worth it, so a click during your own swing cannot
      chain a blow past it. How: a tiny behaviour sits first in the mission's behaviour list, so
      it runs right after the game's player controller has written the frame's input, and clears
      only the attack bits (nothing patched, no Harmony). A bow already drawn is never cancelled
      (the pause only ever starts at a loose).
    - **The AI** (`AttackRatePaceHold`, on - the A/B switch of step 5e's "pace hold"): after each
      attack of a tired AI fighter — melee AND ranged, on foot AND mounted (horse archers and
      lancers too) — no new attack for the pause, **guard really up** (step 16, BATTLE_PACING lever
      #2 - AI_NOTES "Step 16"). How (`AttackRatePaceByInput`, on): a small per-man component sees the
      AI's own input every time the AI decides it (the game's `AgentComponent.OnAIInputSet`, the hook
      RTS Camera Command System uses too) and takes out ONLY the attack bits - his blocks, parries,
      moves, kicks and weapon switches stay his own. When he wants to attack and holds no guard, he
      raises one instead (`AiHoldRaiseGuard`, on - a block; with a shield, the shield); a ready that is
      somehow under way is cancelled with a guard, never released. Never the player, never a man the
      player commands (RTS Camera), never a man with a game job on him (ladder, siege engine, an object).
      Off = step 13's technique, the engine's "no attack" flag - under which held men blocked only
      2-13% (the playtest of 2026-09-27). **The pause survives a step back**: the attacks stay held
      until the later of the pause's end and the step back's end (the old technique: its "no attack"
      waits behind the scripted walk and goes on the moment it ends, if the pause is not over).
    - **The AI's decisions** (`AttackRateAiDecisions`, **off** since step 13 - an A/B switch):
      the AI's chance to attack, to riposte and to loose × m, its aim before a shot ÷ m. On top of
      the timer it double-counts (the playtest log read 128% / 172% "too slow"): the timer runs
      inside the AI's own gap after an attack, and a NoAttack hold already costs the AI its own
      re-decision (1.5-2.6 s measured) when it lifts.
    - **The animations — a little slower when tired** (`AttackAnimationMinPercent`; initial 100,
      shipped 85 since step 20b - Anton after his playtest of 2026-09-28: "swing speed does get
      reduced but to 85%, so swings do show as slower, but not as dramatically as our original
      20%"): the attack animations (wind-up and swing, thrust, bow draw, throw, reload) play at
      **A + (1 − A) × f**, A = this % — full speed at and above the peak line, A at empty, a
      straight line like the run speed (85: ×0.925 halfway, ×0.85 empty). 100 = always full
      speed (step 13's PAUSE ONLY). Steps 13-20 used max(m, A), which with 85 sits at 85% from
      f ≈ 0.81 down. **The pause stays the same in SECONDS**: D is measured at full animation
      speed (each phase's played seconds × the animation multiplier it played at), so the pause
      D × (1/m − 1) is what it was with full-speed animations and the slower swing only adds its
      own extra time to the cycle ("the delay in seconds is nice, leave it be"). The line rides
      on the attack slow-down: with `ExhaustedAttackSpeedPercent` 100 (attacks never slowed) the
      animations stay at full speed too. The slider stops at 5 (the line at 0 would freeze a
      swing at empty). Run speed is not affected by it.
    - **You see it**: the **Attack recovery bar** just above your Athletics bar (§3) empties when
      you attack and fills over your pause, the seconds left inside it; it **flashes** when you
      press attack too early (`FlashBarOnEarlyAttack`).
    - **The swing stack** (step 32, Anton 2026-10-12 - "chain swings stack"; `SwingStackEnabled`, on;
      AI_NOTES "Step 32"). In a tournament 1v1 a single slow swing every ~3 s was blocked ~95% - one
      telegraphed swing is trivially read - and kicks (never held) were the way round it. So the pause
      no longer follows EVERY attack: each fighter **stores swings**, cap = max(`SwingStackMinimum` 2,
      round(his CURRENT points / `SwingStackPointsPerSwing` 20)), rounded half AWAY from zero (7.5 → 8);
      the floor holds even at empty (pool 150 full = 8, at 75 points 4, empty 2; pool 50 full = 3). He
      spawns full; a fresh start (the switch back on, the hideout's boss fight) fills it. An attack -
      whatever the pause covers: a melee release, a loose, a throw - **spends one at its release**; while
      one is stored he is never held (the burst). With none left he waits - **exactly until the next comes
      back**. The refill: one swing per the pause that attack would have left - **D × (1/m − 1) at his
      CURRENT m** (D = his last measured attack's, 0.8 s before any), + for the AI his class's battle-pace
      share - of time **NOT attacking** (the clock stands still during his own ready, release and the reload
      after a loose). Above the peak line the pause is 0: always full. The cap follows his points live: a
      charge that drops it cuts the stored count. So the AVERAGE rate stays step 13's and a rested man can
      save up and burst. **The AI's own gap** (step 21's measurement: after an attack the AI waits about
      `AiMeleeGapSeconds` 1.0 s of its own before the next) is NOT rest: after a melee swing taken from the
      stack the clock waits that long before it runs - without it the stack would refill during his
      natural rhythm and undo the battle pace (a fresh shield man would never be held); at 0 the clock runs
      from the attack's end and his own gap follows the hold, as in step 13. **Kicks and shield bashes**
      (`KicksUseSwings`, on) spend one too and never start a pause by themselves; at 0 they cannot start -
      your kick key (the controller's `EventControlFlag.Kick`, set the frame you press it) is taken out by
      the same input gate, a held AI man's kick out of his own input (step 16's hook - while the AI timer
      holds him BY INPUT; the old NoAttack technique cannot); a bash is the attack button while blocking,
      held with every attack. **The AI** decides nothing new: it may attack whenever a swing is stored; its
      hold (by input or NoAttack) runs only at 0. The brace (step 23) still takes melee attacks out whatever
      the stack. **You**: the hold begins at the release that spends your LAST stored swing (no chained
      blow past it) and its countdown follows your next stored swing every frame (it moves with your m);
      held, the attack fires the moment one is back. Off = step 13's pause after every attack, to the bit;
      the master switch = no stack, no pause, nothing gated. **Shown**: the Attack recovery bar's words read
      **"Swings 4/8"** (stored / cap), its fill is the refill toward the NEXT stored swing (full at the cap),
      the seconds inside only while you wait at 0, the flash on an early press as before. **Measured**: the
      `swing stack` summary lines (attacks from the stack / after waiting / at an empty stack, bursts by
      length, waits, kicks spent / refused, the average fill by f band). **How the attack-rate verdicts read
      now**: they still compare each band's average cycle with the target ÷ m - over a whole fight the
      average holds, but a band filled mostly by a burst reads "too fast" and the waits after it "too slow";
      read them with the swing stack lines (or switch the stack off for an A/B test).
    - **Blocking is never slowed**: weapon handling, shield speed and every defence value of the
      AI are left alone, and with (nearly) full-speed animations a tired man's swing no longer
      keeps him committed much longer (step 20b: at most 1/0.85 ≈ 18% longer at empty).
    - **Measured**: the summary's `attack rate` lines give, per f band, melee and ranged, AI and
      you apart, the animation multiplier asked (step 20b: ×0.85 at empty to ×1.00 at the peak
      line by default) and every phase's average against the peak's (the animations as the engine
      played them), each timer's D (at full animation speed; the played attack beside it when it
      differs), m and pause against the measured gap from the attack's end to the next attack
      (attacks that started inside a timer: must be 0), the cycle, m, the target (the peak's cycle
      ÷ m, + step 20b's slower swing: the band's played attack × (1 − its animation multiplier) -
      the pause does not grow with it, the attack does) and measured ÷ target with a verdict word
      (on target within ±15%, too fast, too slow); your timer (presses
      swallowed, the held button firing, releases); the AI's holds; whether tired men block as
      often as fresh ones. Step 16: the GUARD by state (held by the pause / stepping back / everyone
      else - the held and the stepping-back men must block close to everyone else), each band's cycle
      against the timer's own floor D / m (the attack and its pause - at least ~100% = held as asked;
      step 20b: the attack as played + its pause),
      cycles with a step back inside counted and shown apart, and the input hook's own numbers.
    - **Battle pace — the shield wall swings less, archers shoot slower** (Anton, 2026-09-28: "make the
      infantry more defensive, especially the guys with the shields, so that maybe they swing 30% less (and make
      that adjustable)" and "make the archers a bit slower ... about 30% slower overall"; built in step 21 - AI_NOTES
      "Step 21"). The AI's pause after each attack gets a SHARE on top of the tired part, by the attack's CLASS -
      read at each release from what the fighter really holds (a shield in his other hand, a bow or a crossbow),
      never from his formation or troop type; AI heroes, companions and lords are AI fighters like any other:
      - **Shield infantry** (melee on foot with a shield in the other hand): `ShieldInfantrySwingsLessPercent` (30)
        % fewer swings; **other foot melee** (two-handers, polearms, a one-hander alone): `FootMeleeSwingsLessPercent`
        (15). Fresh or tired alike, on top of tiredness (multiplicative): the pause stretches his EXPECTED cycle - his
        attack D + his tired pause T + his own gap G (`AiMeleeGapSeconds`, 1.0 s) - by 1 / (1 − q), q = % / 100:
        **pause = (T + q × (D + G)) / (1 − q)**, so D + pause + G = (D + G + T) / (1 − q) - at every tiredness he
        swings q fewer than tiredness alone lets him. Why not D × (1/(m (1 − q)) − 1): the AI does not attack the
        moment it may - Anton's logs measured a fresh AI melee cycle of 1.80 s on a 0.78 s attack, and a pause ADDS
        to his own ~1.0 s gap - so a share sized on D alone would buy about half the asked cut (16% for 30%). A fresh
        shield man waits about 0.76 s after each swing, guard up.
      - **Bowmen** (a bow, on foot and horse archers): `ExtraPauseAfterBowShotSeconds` (2.0) more seconds after each
        shot; **crossbowmen** `ExtraPauseAfterCrossbowShotSeconds` (2.5) - on top of the tired pause, sized for about
        30% fewer shots on the measured fresh cycles (bows ~4.5 s, crossbows ~6 s; the extra ≈ cycle × (1/0.7 − 1)).
      - **No share**: riders' melee (lancers, horsemen), thrown weapons (javelins, throwing axes and knives, stones),
        slings - and **you**, never (your timer stays pure tiredness). Kicks and bashes start no pause, as before.
      - It rides on the AI timer (`AttackRatePaceHold`; off = no AI pause at all) and its technique (by input, guard
        up), so everything of the tired pause holds for it: the master switch (off = every pause lifted at once),
        a step back never cancels it, the hideout boss fight's fresh start ends it, a game job refuses it, RTS
        Camera's AI-driven hero is you. Hot swap: read at each attack's end - a change applies at every fighter's next
        attack; a pause already running keeps its length. 0 = that share off.
      - **Measured**: the summary's "battle pace" lines give per class the attacks and the men, the pauses by reason
        (the share alone at full strength, tired + the share, tired only), the tired part and the share, the measured
        cycle = attacks a minute per man while fighting, the AI's own gap after a pause, and for foot melee the
        model's cycle with the setting at 0 and with it (his attack + the pause + G) against the measured one. The
        real check is the same battle with the sliders at 0 and at their values (PLAYTEST).
    - **The shield wall holds its slots** (Anton, 2026-10-17, a playtest: a fresh shield wall waiting for the enemy - "men
      step 2-3 m forward out of the wall to swing at an enemy, then walk back to their slot ... every man does this, so the
      wall bulges and breaks"; he wants them to keep their slot "the way Realistic Battle Mod does it"; built in step 41 -
      AI_NOTES "Step 41", RESEARCH §O; `ShieldWallHoldsSlots`, on). Vanilla tells the engine "keep with the formation
      instead of moving to the agent" only for a man with a shield in the 5th slot of a mostly RANGED formation - for a
      shield-wall infantryman never, so the native AI walks him at the enemy he targets. The mod sets that flag for:
      - **who**: AI men (never you, never a man you drive), on foot, in a formation and not detached from it, whose formation
        is in a **shield wall, square or circle** (the game's three defensive arrangements), under **any order but a charge**
        (charge, charge a target, the AI's tactical charge, attacking a gate) **or a retreat** - hold / halt, move, advance,
        fall back, follow all hold; not a man fleeing; both sides alike;
      - **the escape**: a man FAR from his slot (6 × the formation's speed - vanilla's own catch-up radius, ~20 m) AND
        BEHIND the line is left to vanilla so he may run to rejoin; a man far AHEAD of his slot is held (walked back);
      - **where**: field battles, custom battles, hideouts, sieges for the DEFENDERS; not a siege's attackers (their climb
        and gate orders are moves - RBM's lesson: held to their slots they stood on the wall top), not naval battles;
      - **what is written**: vanilla's own call with vanilla's own numbers (the slot vanilla itself computes, the formation's
        facing and integrity data) and the flag true - right after each of vanilla's own updates (its per-man timer,
        ~0.5 s) and at a staggered look every 0.25 s; nothing else changes (no speed, no behaviour weights, no Harmony);
      - **live**: off (or the master switch off) writes vanilla's own value back for every held man at once - vanilla would
        not under a halt order (it writes nothing there);
      - **measured**: the summary's "wall" lines - every man of a holding shield wall / square / circle sampled every 0.5 s:
        the share more than 1 m and 2 m AHEAD of his slot along the facing, the mean and farthest, all and in contact (an
        enemy within 5 m), split ON / OFF by the switch at the sample, so one battle with a mid-battle switch reads as an A/B.
      - **a small step out - the leash** (Anton, 2026-10-17, his playtest of step 41: the wall held, but the men "looked many
        times like they were about to reach to hit but stopped, maybe as the enemy took a few steps back, so they very rarely
        hit" - "give them just a bit of a step if the enemy moves a small step back, but not as far"; built in step 42 - AI_NOTES
        "Step 42"; `ShieldWallStepOutMetres` L, 1.0 m): a held man whose enemy (the AI's own target, else his immediate enemy)
        stands close in front of his SLOT - in front of its line and within L + 2 m of it (a strike's reach from a step of at most
        L) - is let free (vanilla's own value) so he closes and swings; he is held again the moment he stands L out of his slot or
        no enemy is close (+ 0.5 m of grace while he is out), and walks back; he may step again only once back within 0.3 m (at
        most half the leash) of his slot. "Out" = along the facing for a shield wall, away from the formation's centre for a square
        or circle (their sides and backs face outwards). A man out is looked at every 0.1 s (the leash bites within ~0.1-0.3 m of
        L); held men as above. 0 = the hard hold exactly. Live: a new L applies at the next look (≤ 0.1 s for a man out). Measured:
        the summary's "wall step out" line - steps by men, a man's steps a minute while held, each step's farthest out, how the
        steps ended, the overshoot past L, back in the slot, and in-contact samples past L and past L + 0.5 m.
      - **the wall pushes as one** (step 68, Toni's 100 v 100 of 2026-10-19: his shield wall ordered forward with F1 → F4 =
        ADVANCE had 15.4% of its men in contact more than 1 m ahead of their slots, the farthest 6.2 m - the looter battle with the
        wall standing 0.8%; "they need to hold the wall and push the whole wall, not to break up"; AI_NOTES "Step 68", decision
        61; `ShieldWallAdvanceAsOne`, on): under an advance the game's ORDER slot of each man lies at the enemy's line
        (`MovementOrder.GetPositionAux`: the nearest enemy formation's median less 2 m), and every AI man walks to his order slot
        (`HumanAIComponent.GetFormationFrame` → `Agent.TrySetFormationFrame`, every ~0.5 s) - while step 41's flag and the
        "ahead" measure use his slot in the line AS IT STANDS (the men's average + his place). Under a stop the two are the same
        point; under an advance the men free to move walk on into the enemy while their mates fight, and the line stretches. So
        for a held man under a MOVING order (advance - yours or the AI's -, move, fall back, follow; never a stop / hold; a charge
        still lets the wall go) whose formation's front is within `ShieldWallAdvanceNearMetres` (10 m) of the nearest enemy
        formation's front, the frame the engine walks him to is his slot in the line + at most `ShieldWallAdvanceStepMetres`
        (1 m) toward the game's order slot - every man the same step, so the line advances as one body at the pace its fighting
        front allows; written with vanilla's own call right after vanilla's own update and at step 41's looks (a man stepping out
        on the leash keeps it; the leash still lets him strike); off, a let-go, the hold off or the master switch = the game's
        own frame back at once. Farther from the enemy the game's own march. Measured: the [summary] "wall slots ON by order"
        line (the in-contact leak for standing / ADVANCE / moving) and "wall advance as one" (paced writes and men, how far the
        game's slot lay, the looks not paced by reason, frames given back).
      - **back to the line after a kick** (step 68; `ShieldWallKickReturnMetres` 0.5 m): a held man whose OWN kick / bash began
        within 2.5 s, or who presses a punish window (step 54's step-in), is out of place from 0.5 m ahead of his slot (step 63's
        mark is 1.25 m) and walked back AT ONCE - step 63's backpedal, no 0.5 s wait - as soon as he can move (not down, reeling,
        or in his kick), to within 0.25 m. 0 = step 63's rule only. The "wall pulled back" line counts these spells apart.
      - **a standing wall stands firm** (step 75, Toni 2026-10-22: "the men in the shield wall still step forward even when on a
        standing order, they take a few steps to strike at an enemy, then fall back"; `ShieldWallStandFirm`, on, the PLAYER page;
        decision 69): a man held above whose formation is on a STOP, or on a MOVE that has reached its place (within
        `ShieldWallStandFirmArrivedMetres` 2 m of its order position, its men moving at most `ShieldWallStandFirmStillMetresPerSecond`
        0.5 m/s - latched until a new place or pushed 2 x that away), never leaves his slot: the leash is 0 for him, a running sprint
        ends, step 68's pace never applies, the engine's walk target is his slot (`ShieldWallStandFirmPinFrame`) and its tolerance
        the slack (`ShieldWallStandFirmTightTolerance`); more than `ShieldWallStandFirmSlackMetres` 0.3 m ahead of his slot he is walked
        straight back to within half of it (step 63's backpedal, at once, never while his kick plays). He keeps fighting from his slot:
        guard, blocks, swings and kicks / bashes at what is in reach. Never under an advance, a charge, fall back, follow or a retreat;
        a broken wall (step 67) and a man far behind still let go. Measured: the [summary] "wall stands firm" line (ON vs OFF in
        contact, strikes from the slot, the levers refused by kind).
      - **spearmen keep their spears, the second rank thrusts past** (step 76, Toni 2026-10-22: "my shield wall-ers have spears but
        they keep using their swords, even though the spear has a longer reach"; `SpearPreferenceEnabled`, on, the PLAYER page;
        `ThrustsPassFriends`, on, Advanced; decision 70): RBM's weapon preference without Harmony - an AI man carrying a melee polearm
        and a melee sidearm favours the polearm x35 (`AiWeaponFavorMultiplierPolearm`) while no enemy is close and the sidearm x55
        (`...Melee`) once one has been close 2 s, the polearm again 4 s (mounted 2 s) after the last; close = within 1.0 m + 60% of his
        spear's length (1.5-3.5 m), not running away, not wielding a polearm himself; a back-rank man of a wall / a halted line keeps the
        polearm; the stat decorator writes the favor from a flag the tick sets. A THRUST into a friend carries on (the damage decorator's
        `DecideWeaponCollisionReaction` → `ContinueChecking`); swings / kicks / bashes still bounce; the friendly-hit stun 0.4 → 0.1 s.
        Measured: the [summary] "spears" lines (time holding the polearm / the sidearm by what was close, ON vs OFF) and "thrusts past
        friends" (the same man then hitting an enemy within 0.5 s, the friend's flinch).
    - **Brace by orders — tired AI men stop swinging and defend** (Anton, 2026-09-29: "soldiers get to 0% athletics
      but they still swing as much as they can - make soldiers not swing but only defend when they have reached a
      certain floor, that depends on their current orders, until they have replenished the floor +20%" ... "when
      defending they whip out their shields if they have one"; the spread: "+- 5% additive to those 20% he has to wait
      per soldier (once rolled on a battle, adds some bravery-like randomness)"; built in step 23 - AI_NOTES "Step
      23", `BraceEnabled`, on). The goal: lines attack at first, then brace; with blocks costing Athletics (step 22)
      they hold more and swing less.
      - **The bar** is his Athletics as a share of his FULL pool - exactly what his bar's fill shows (not f).
      - **The floor comes from his formation's movement order now** (`Formation.GetReadonlyMovementOrderReference()`,
        yours or the enemy's team AI alike): **charge** (charge, charge a target, attack a gate, the AI's tactical
        charge) `BraceFloorChargePercent` (20); **advance** (your Advance, or an AI formation walking into the enemy
        on a Move order while its behaviour is an advance) `BraceFloorAdvancePercent` (40); **everything else** -
        hold / stop, move to a position, retreat, fall back, follow, no order -
        `BraceFloorHoldPercent` (60); **no formation** - tournament and arena fighters, duels, anyone the game put in
        no formation - `BraceFloorNoFormationPercent` (20; step 28, Anton 2026-10-12: a tournament 1v1 on the hold
        floor braced 62% of its time, the AI blocking ~95% of his swings).
      - **Each man's own floor** (step 28): his order's floor ± his own offset, a uniform roll of up to
        `BraceFloorSpreadPercent` (10) points made ONCE per man and battle with its own dice - independent of the
        margin's roll below; the slider rescales everyone's roll at once; clamped to 0-100. Everywhere (every order,
        no formation too), so a line never braces as one man.
      - **At or below his floor he BRACES**: no melee attacks - step 16's input component takes the attack bits out
        of his own input and raises a guard when he wants to attack (a ready under way is cancelled, never
        released); his own blocks, parries and moves stay his. **Until his bar is back to floor + his margin**: the
        margin is `BraceRecoverPercent` (20) ± his own offset, a uniform roll of up to `BraceRecoverSpreadPercent`
        (5) points made ONCE per man and battle (a slider change rescales everyone's roll at once; the target is
        never below floor + 1). Holding 60 → 80 (± 5), advancing 40 → 60, charging 20 → 40, no formation 20 → 40 -
        each from his own floor (± 10), so e.g. a holding man with +4 braces at 64 and attacks again at 84 (± 5).
      - **The order may change mid-brace**: the new floor applies at his next look (a charge order at 55% ends a
        hold brace at once - "the order changed"; a hold order at 35% starts one at once).
      - **Wounds**: his target is capped at the top his wounds let him refill to, and under that cap the band keeps
        its width (floor = min(order floor, cap − margin), from empty at the bottom) - a wounded man never braces
        at a bar full to his cap and always reaches his target by refilling ("his wound cap"). Never stuck.
      - **Ranged attacks go on**: a bow, crossbow or throwing weapon in his hand (read about every 0.25 s) or a
        ranged action under way is let through. **The shield comes out** (`BraceWieldShield`, on): at the brace's
        start, a man who carries a shield not in his hand takes it out (`Agent.TryToWieldWeaponInSlot`), a
        one-handed weapon first if his weapon needs both hands; a man with a ranged weapon in hand, or a two-hander
        and no one-hander, is left alone; checked every 1 s while he braces, at most 3 wield calls a brace (the AI
        may put it away again - counted). **It stays up** (`BraceRaiseShield`, on): while he braces with a shield
        in hand, a guard (`DefendDown`) is held whenever his own AI holds none. When the brace ends nothing is
        forced back - his AI picks his weapons.
      - **Who**: every AI fighter on foot or mounted, AI heroes and companions too; **never you** (nor a soldier
        you took over with RTS Camera - his brace ends). Looked at about every 0.25 s, staggered over the ticks.
      - **It stacks** with the attack pause (step 13), battle pace (step 21) and the step back (step 16): each is
        its own wish on the same input component, and the attacks stay out while ANY holds him - one ending never
        lifts another. The hideout boss fight's fresh start (step 19) ends it. The master switch, `AthleticsEnabled`
        and `BraceEnabled` lift every brace at once. Hot swap: the floors and margins are read at every look.
      - **Measured**: the summary's "brace" lines - the men who braced, braces by order, the time bracing (total,
        per brace, per man, the share of the AI fighters' time), the brace lengths and how many still braced when
        the battle ended (the "do lines turtle forever" check), the ends by reason, the margins rolled and the
        brace length by margin, the shield (taken out, seen in hand, put away by the AI), and the GUARD while
        bracing (blocks vs hits) with the attacks that still started (melee should be 0; ranged allowed).
  - **Tired AI blocks in a random direction** (step 30, Anton 2026-10-12, after a tournament 1v1 where the
    exhausted AI blocked ~95% of his swings - "crazy good AIs"): the more exhausted an AI man is, the more his
    block DIRECTION is a guess. Weight w = `TiredBlockRandomPercent` (100) / 100 × (1 − f): at or above the peak
    line his own AI picks; at f 0.5 half his picks are his own and half a uniformly random direction; empty, every
    pick is random (right 1 in 4 by luck). A wrong guess with a shield is a wrong-side block (5 points, and it
    restarts his refill delay - step 29), with a weapon the blow lands. Built on the GAME'S OWN randomiser, the
    per-agent `AiRandomizedDefendDirectionChance` (vanilla 1 − (AI level)³ - near 1 for a recruit, below 0 for a
    final-round tournament fighter): the stat decorator writes own + (1 − own) × w (own clamped to 0-1), so his own
    AI's picks keep their own randomness and ours is on top. w moves in the speed multipliers' recompute steps
    (0.05); w 0 leaves the game's value. AI only, on foot and mounted; never the player (nor the hero RTS Camera's
    AI drives for him); the forced guards of steps 16 / 23 (`DefendDown` when he holds none) are ours and stay
    fixed. Off at 0, with AthleticsEnabled, and with the master switch. **Measured**: the summary's "tired block
    direction" line (men, recomputes, his own → written chance by f band) and "AI guard by f" (blocked % - shield
    right side / WRONG side / parry - vs landed, by f band): the empty band should block far worse than the fresh.
  - **Run speed on foot** = M + (1 − M) × f, M = `MinMoveSpeedMultiplier` (initial 0.7 - an empty
    man runs at 70% of his pace; step 14, Anton after his 240v240: "make them slow down to 70% speed",
    the 0.3 of steps 5c-13 was "too slow, unrealistic"; shipped **0.6** since step 20b - Anton after
    his playtest of 2026-09-28: "speed (run) floor sweetspot is 60% when their athletics is at 0%"; shipped
    **0.3** again since step 22 - Anton, 2026-09-29, for slower battles: "lower the floor max speed they can
    run with when they get exhausted to 30% again").
    Tired men slow down, so fresher men
    overtake them. Horses keep their speed (Anton's pick):
    `MountMinSpeedMultiplier` (1.0 = unaffected; lower it to let a tired rider's horse slow on
    the same curve).
- **Regeneration** starts after `RegenDelayBlowTimes` (2) × `BlowTimeSeconds` (1.5 s) with no
  attack, and follows EFFORT = speed ÷ the fighter's current top speed (the horse's for
  riders). Standing or walking — effort up to `WalkEffortFraction` (0.4) — refills from empty
  to full in `FullRegenSecondsStanding` (60 s). Faster than a walk the rate falls in a
  straight line to × `RegenMultiplierAtFullRun` (0.5) at top speed (120 s from empty to full
  at a flat-out run). Never above the health cap. Any new blow stops regeneration and
  restarts the delay. (0.4 because the game walks people at 1.8 m/s and their top speed on
  foot works out at about 4–5 m/s — RESEARCH §D. "Current" top speed: a tired man's top is
  lower, so keeping up with a walking formation is harder work for him.)
  - **Faster when low, slower when full** (step 14, Anton: "recover faster when it's low and
    slower as it is fuller by some modifier, not crazy, maybe half linear"). The rate is a straight
    line in the fill x (Athletics ÷ the FULL pool): rate(x) = r0 × (1 − (1 − k) × x), k =
    `RegenRateNearFullPercent` (initial 50) / 100 - near full the bar refills at half its speed near empty.
    **Shipped 100 since step 22** (Anton, 2026-09-29, for slower battles: "drop the faster athletics
    increase when empty - return it to fully linear again all the way, 100% for 60 sec"): the same rate
    all the way, empty → full in exactly 60 s at a walk (half in 30 s, the peak line in 45 s) - the curve
    below stays one slider away.
    r0 keeps `FullRegenSecondsStanding` meaning "empty to full at a walk or slower":
    r0 = ln(1/k) / ((1 − k) × T) of the pool per second (k = 1 → 1/T, the flat refill of steps
    5c-13). The effort multiplier above multiplies on top (a flat-out run still takes twice as
    long), and the health cap still caps where it stops. At k 0.5, T 60 s, at a walk:

    | From empty to | Flat (k 1) | Curve (k 0.5) |
    |---|---|---|
    | half the bar | 30 s | ~25 s |
    | the peak line (75%) | 45 s | ~41 s |
    | full | 60 s | 60 s |
    | the last quarter alone (75% → full) | 15 s | ~19 s |

    So the rate at empty is ×1.39 the flat rate, near full ×0.69. A tired man gets back to full
    strength about 4 s sooner and tops off the last quarter about 4 s slower. The curve is on the
    FULL pool, so a wounded man refilling to his cap (say 50%) refills on its fast part.
- **A fresh start in a hideout's boss fight** (Anton, 2026-09-28: "they will come fresh and we will be
  tired"; built in step 19, `HideoutBossFightRefill`, on). When the last bandit of a hideout's first fight
  falls, the boss comes out with his men - a short cutscene, then a talk: a duel ("Very well.") or everyone
  fights ("I don't fight duels with brigands."). The moment that fight BEGINS, every living fighter on the
  player's side refills at once to a full Athletics bar - up to the health cap, so wounds still cap it and a
  badly wounded man still cannot reach full strength - and whatever runs on them ends: the attack pause
  (yours and the AI's, a queued one too), a step back. In a **duel** the player's men stand aside (the game
  takes them off his team), so only the player refills; in the **battle** the player and every man still
  standing. The boss's side is not touched: the game spawns the boss and his men during the cutscene, so they
  start full like every new fighter. Where it is seen: the game starts its "Win the Duel" / "Win the Fight"
  objective at that very moment, in both kinds of hideout (AI_NOTES "Step 19"). Gated by the master switch
  and `AthleticsEnabled` like everything else.
- **Cavalry**: riders use the very same pool (their Athletics skill). Riding never drains
  it; only blows do. The horse has no Athletics of its own.
- **The sprint** (step 33, Anton 2026-10-12 - "a short burst of speed, meant to feel a little like a dodge, and
  for the AI to make charges sharper"; `SprintEnabled`, on; AI_NOTES "Step 33", RESEARCH §N). Every fighter on
  foot stores **seconds of sprint = max(`SprintMinSeconds`, his CURRENT Athletics points ÷ `SprintPointsPerSecond`)**
  (2 s, 20): 150 points = 7.5 s, 100 = 5 s, 40 or fewer = the 2 s floor - an exhausted man still has a dodge (step 35,
  Anton 2026-10-12: "when tired the store collapsed"; step 33 had empty = 0, which `SprintMinSeconds` 0 restores); when
  his points drop the stored seconds are clamped to the new size. The refill rule is the same for the floor: the whole
  current store in `SprintRefillSeconds` ÷ the factors, so a tired man's 2 s refill slowly (14 s standing at an empty bar). **Refill** (only while not sprinting): empty → full in `SprintRefillSeconds` (7 s)
  standing or walking at / above the peak line; × `SprintRefillMovingFactor` (0.5) faster than a walk (effort
  above `WalkEffortFraction` - the Athletics refill's notion); × a tiredness line from 1 at the peak line to
  `SprintRefillExhaustedFactor` (0.5) at an empty bar; the two multiply (7 / 14 / 14 / 28 s). **A start** needs
  at least `SprintMinStartPercent` (20) of the store (no spamming tiny bursts). **The speed** (step 35 - Anton's
  tournaments, 2026-10-12: "the sprint felt very slow"): the sprint's length T = the seconds stored at its start (the
  AI: its share of them); for the first `SprintFullSpeedSharePercent` (30) % of T he runs at `SprintMaxMultiplier`
  (1.6; defaults.json 2.0) × his **FRESH** run speed - whatever his Athletics (step 33 had × his CURRENT run, so at the
  shipped run floor 0.3 an exhausted man's sprint was 0.3 × 1.6 = 0.48 of a fresh pace) - then a straight line to
  `SprintMinMultiplier` (1.1) × his **CURRENT** run speed at T (a fresh man 2.0 → 1.1, an exhausted one 2.0 → 0.33). A
  partial store still starts at the peak (the dodge). **Acceleration** (step 35): the game's foot
  `TopSpeedReachDuration` (DrivenProperty 86, per agent; Sandbox: 2.5 + max(5 − (1 + skill × 0.01), 1) / 3.5 −
  min(weight / (weight + gear), 0.8) ≈ 2.3-2.8 s) is longer than a short sprint's full-speed part - Anton's 0.4-0.8 s taps
  never got going; while he sprints it is min(the game's, `SprintTopSpeedReachSeconds`) (1 s; 0 = the game's own), back to
  the game's when the sprint ends. The sprint's clock and the drain (1 s a second) run **only while he
  moves** (above 0.5 m/s - plumbing). While sprinting his **Athletics does not refill**; no other cost. **On foot
  only** (a horse refuses it, mounting ends it). The lever is the run property the tiredness already scales:
  `MaxSpeedMultiplier` × the sprint's run factor IN PLACE OF the tiredness run multiplier (step 35; quantised in 0.05
  steps - each a properties recompute through the 50-a-tick budget - and never below 0.05), and the same recompute caps
  `TopSpeedReachDuration`. **You**: hold `SprintKey` (default **LeftControl** - free
  in the game's fight keys; a key NAME, any of Core `KeyNames`) in a fight, the order menu closed, your own
  hands (not while RTS Camera's AI drives your hero). A PRESS starts it (a held key never restarts it after the
  store ran dry); letting go ends it - the rest stays stored. **The AI** (`SprintAiEnabled`, on): every
  formation's movement-order kind is read every 0.25 s (step 23's read - the AI's own advance / tactical charge on
  a Move included); a kind that differs from the last look is a NEW order, and each AI man on foot in that
  formation sprints once. Step 36 (Anton: shock troops moved behind the enemy must still have their sprint for the
  real charge), simplified by step 37 (Anton: one number per order): each order kind has a RESERVE - the KEEP, % of his
  FULL store (its capacity now) - and he sprints everything he holds above it: stored - keep x capacity seconds -
  nothing below 0.2 s (plumbing) and no `SprintMinStartPercent` gate for the AI (the reserve is its gate). Charge
  (charge, charge a target, attack a gate, the AI's tactical charge): `SprintAiChargeKeepPercent` 0 - everything.
  Advance (F1 F4 "engage", the AI's own advance): `SprintAiAdvanceKeepPercent` 50 - from 100% he sprints 50%, from 90%
  40%, from 60% 10%, from 45% none. Move (a plain move to a position, F1 F1, or an AI general's Move under any other
  behaviour): `SprintAiMoveKeepPercent` 80 - from 100% 20%, from 90% 10% (Anton's example: down to 80%), from 60% none.
  So men moved or advanced into position still have sprint left for the real charge. Hold / stop, retreat, fall back,
  follow (a man or an entity), no order: none. He stops early when an enemy is within
  3 m (plumbing) or the order changes again. Never you or a hero you drive; brace, step back and the pause are
  not touched. Whole formations sprint together on purpose: the game caps a formation man's speed against his
  formation's average top (cached ~2 s), so one sprinter alone would be held to the others' pace. **Off** (the
  master switch, `AthleticsEnabled`, `SprintEnabled`) = no sprint, every running one ended, speeds back on the
  next recomputes. A hideout's boss fight (step 19) fills the stores of the side it refills.
- **Aggression and the fever** (step 43, Anton 2026-10-17: "people are defensive by nature - they don't want to throw
  their life away ... when the general turns to them and says NOW!, everybody surges ... the more experienced they are
  the longer they follow that order, and the more tired they are ... the shorter they fight hard. The surge fades." -
  and, redesigning the first draft's flat calm factor: "a man at work doesn't hold a pace until he drops; the more tired
  he is, the less he swings". AI_NOTES "Step 43"; Core `FeverMath`, `FeverStore`).
  - **Aggression follows Athletics**: every AI man ON FOOT's two melee attack-decision values - `AIAttackOnDecideChance`
    (the game's chance to start an attack at a combat decision, 0.1 × 0.48 × (3 − defensiveness) clamped 0.05-1: a
    charge 0.144, other orders 0.096, a shield wall / square / circle 0.05) and `AIAttackOnParryChance` (the riposte,
    0.08 − 0.02 × defensiveness) - × his SHARE: `AiAggressionFreshPercent` (60) at a full bar, a straight line in the bar
    to `AiAggressionExhaustedPercent` (20) at an empty one. The bar = points ÷ his FULL pool (the brace's reading, not f -
    so it slides over the whole bar); a wound's cap is in that reading already (no special case: a wounded man is less
    eager because his bar is lower). Written by the stat decorator after the base model's `SetAiRelatedProperties` (which
    assigns both with `=` every recompute: never compounds), the share in 0.05 steps carried by the speeds' re-target
    (every charge, refill step, wound and settings change) under the same 50-a-tick recompute budget. No floor of ours
    (the game's 0.05 is its own formula's clamp; 20% means five times rarer - a tired shield wall's 0.05 → 0.01).
    Everyone, always - tournaments, arenas and duels too. Never the player; never riders (a rider's swing comes with his
    pass - a less eager rider would ride past without striking; the game's values for them); never ranged (`AiShootFreq`,
    the aim: archers have step 21's wait). With `AttackRateAiDecisions` (step 5e, off) on, both multiply: the game's × m ×
    share. A tired man now has TWO brakes - fewer decisions to attack AND the pause after each attack (unchanged, as are
    step 21's shares, the swing stack and the brace floors; Anton playtests the balance first). 100 / 100 = the game's AI.
  - **The fever**: a NEW charge or advance order on his formation (the sprint's order poll - step 33/36: charge, charge a
    target, attack a gate, the AI's tactical charge on a Move; advance, the AI's advance on a Move; the player's F1+F3 /
    F1+F4 and the AI general's alike) ARMS each AI man on foot with a store of his CURRENT Athletics points ÷
    `FeverPointsPerSecond` (10) seconds - a 100-point man at 80 points: 8 s. Its peak is ABSOLUTE,
    `FeverAggressionPercent` (80% of the game's values): while armed his share = max(his tired share, the peak); while
    burning it fades in a straight line from the peak to his CURRENT tired share as the store empties - tired + (peak −
    tired) × left ÷ full, never below tired ("tired" itself keeps sinking as he fights). A man already above the peak gets
    nothing from it. The clock starts only at his first enemy within `FeverContactMetres` (5 m; looked at every ~0.25 s,
    staggered) - an AI general orders the charge from far off; once burning it burns to empty, contact or not. It burns
    × 1 ÷ `FeverShieldDurationMultiplier` (0.7) while a shield is in his off hand, read live at every look (a shield taken
    from the back, or put away for a two-hander, switches the rate). The same order kind again (charge → charge to a
    target) leaves a running fever alone; the other aggressive kind (advance → charge, charge → advance) re-arms it to the
    larger of what is left and a fresh fever (a re-arm while burning keeps burning). A new order of any other kind (hold,
    stop, move, retreat, fall back, follow) ends it, armed or burning - the general called them back. No formation
    (tournaments, arenas, duels): no orders, no fever - the bar's share only. Never the player, never riders (a man who
    mounts loses his fever). Heroes: the same rules (their bigger pools already make their fevers longer).
  - **The fever sprint** (`FeverSprintToClose`, on; Anton: "they might sometimes use their sprint to close" - then: the
    sprint fades with the fever): while armed or burning, every `FeverSprintCheckSeconds` (1 s) a man with sprint stored
    above his order's reserve (step 37), not sprinting, an enemy at least `FeverSprintMinMetres` (3) and within
    `FeverSprintMaxMetres` (12 m), rolls with chance `FeverSprintChancePercent` (50) × left ÷ full (full while armed) -
    several per fever, rarer as it fades; a start is step 36's AI sprint (everything above the reserve; it ends at 3 m
    like any). Needs `SprintEnabled` and `SprintAiEnabled`.
  - **What it does not touch**: the brace (a bracing man's attack bits stay out of his input - a fever never overrides
    it); step 21's battle pace and step 32's swing stack time what happens AFTER an attack (the share changes how often
    he DECIDES to start one); step 41/42's wall hold (a wall under a charge is not held anyway).
  - **Off**: the master switch or `AthleticsEnabled` = the game's own values at the next recompute (the decorator's gate),
    every fever ended; `FeverEnabled` off = the bar's share only, every fever ended at once.
- **Courage and cohesion** (step 49, Anton 2026-10-17 night - AI_NOTES "Step 49", decision 41): every AI man on foot in a formation is
  looked at every ~0.5 s (staggered): the tracked men within `CourageRadiusMetres` (6 m; full weight to half of it, a line to 0 at its edge),
  each by his TOUGHNESS = (1 + `CourageArmourWeightPercent` 200% × armour / 100) × (1 + `CourageLevelWeightPercent` 100% × min(level, 60) / 30)
  × (1 − `CourageHealthWeightPercent` 50% × the health he lost) - armour = the kit's head + body + legs + arms, read once; an ENEMY carrying a
  two-hander or polearm and no shield × `CourageHeavyWeaponPercent` (150%). Local odds = his side's sum (him included) ÷ the enemy's. He needs
  `CourageBaseOddsPercent` (100%) × (1 + `CourageTiredWeightPercent` × the bar spent - Athletics on only) × (1 + `CourageMoraleWeightPercent`
  × max(0, 50 − morale) / 50) × (1 + `CourageShockWeightPercent` × min(landed hits within `CourageShockSeconds`, 3) / 3 - + live impact
  outcomes once a family applies) × (1 − (1 − `CourageFeverPercent` 40%) × the fever's share) × (`CourageHeroPercent` 75% for a hero). Below
  it he is TIMID: far from his target (> `CourageRegroupRadiusMetres` 5) AND short of mates (< `CourageRegroupMates` 4 - twice / half the
  radius when shocked) he WALKS BACK through his own input (step 16's backpedal, a `Regroup` wish - facing his AI's target, his guard and
  attacks his own) to the nearest BIGGER group of his side (≥ `CourageFlockMinMen` within `CourageFlockRadiusMetres`), else his formation's
  centre of mass (`CachedAveragePosition`); leaning away from the enemy, round them when they stand between; he sprints when the target is
  beyond `CourageSprintMetres` or `CourageSprintEnemies` enemies are near (step 33's AI sprint above `CourageSprintKeepPercent`); in a PANIC
  (shocked, odds below `CouragePanicOddsPercent` of the required) at once, keeping `CouragePanicSprintKeepPercent` (0). Among his mates he
  HOLDS. While timid (`CourageTightenWeights`) his Melee weight wins only within `CourageTimidMeleeMetres` (2 m), re-applied every look (the
  game resets them on orders), the game's back when brave. Brave again at ≥ required × (1 + `CourageHysteresisPercent`) after
  `CourageMinTimidSeconds`, and only once a walk is over. Never the player, riders, men held in a shield wall (step 41), busy / detached /
  routing men, a siege's attackers, no formation; no tournaments, arenas, naval. Works without the Athletics system (no tiredness, no
  sprint). Master switch FIRST, then `CourageEnabled`: off = everyone let go at once (the game's weights back); the looks still MEASURE, so
  the summary's "courage" lines compare isolated men and their distance from the formation's centre ON vs OFF. Step 49b: a man in his FEVER
  (armed or burning) who is NOT shocked is left alone - courage never makes him timid (a timid spell ends, `CourageFeverPercent` then applies
  only to a shocked feverish man); a timid, unshocked man with ≥ `CourageRegroupMates` mates and an enemy within 8 m rolls
  `CourageSurgeChancePercent` × his bar at each look for a `CourageSurgeSeconds` SURGE (brave, the game's weights, no walk); the timid Melee
  weight is 10 flat to his reach (full within it); the summary adds fever presses, surges and the STANDOFF (timid men 3-8 m from an enemy,
  no attack by either in 10 s).
- **The fear flock** (step 67, Anton 2026-10-19 - AI_NOTES "Step 67", decision 60; `Missions/AthleticsLogic.Fear.cs`, `Missions/FearBody.cs`,
  Core `FearFlock.cs` / `FearFlockStats.cs`): courage's evolution - it rides courage's look (every AI man on foot in a formation, ~0.5 s,
  staggered) and its scan. His FEAR = 1 − (1 − `FearMoraleWeightPercent` × m)(1 − `FearTiredWeightPercent` × t) - m = (`FearMoraleCalm` 60 −
  morale) ÷ (60 − `FearMoraleBroken` 15), t = (`FearFreshPercent` 75 % − his bar) ÷ 75 %, both clamped 0-1 (Athletics off: t = 0; no morale: m =
  0) - × (1 − DISCIPLINE), DISCIPLINE = `DisciplineMaxPercent` 70 % × clamp((level − `DisciplineLevelLow` 6) ÷ (`DisciplineLevelHigh` 26 − 6)) (a
  hero the full 70 %), + `FearThinLinePercent` 25 % × (1 − d) while an enemy is within 8 m and no comrade stands behind him (0.3-3 m back along
  the line from the nearest enemy, 1.5 m aside); mixed (1 − u) his own + u his unit's mean (`FearUnitSharePercent` 50, the last 0.5 s of looks).
  The PULL p = clamp((fear − `FearStartPercent` 15 %) ÷ (`FearFullPercent` 80 % − 15 %)) - below the start nothing (a flocking man calms below
  10 %). The unit's SHELTER (a table of 8 teams × 16 formations, published every 0.5 s) starts at its centre of mass and moves toward the centre +
  away-from-the-enemy-near-the-unit × `FearShelterBackMetres` 4 × the unit's mean pull - freely forward and sideways, backward at most
  `FearDriftMetresPerMinute` 6 m a minute (× `FearDriftHoldPercent` 25 % under a hold / move / follow or in a shield wall, square or circle).
  His PLACE = his slot + p × (the shelter − his slot), never more than `FearShelterMaxMetres` 12 from the slot; with no comrade between him and the
  nearest enemy (within `FearCoverWidthMetres` 0.9 of the line) it leans p of the way to the spot 1 m behind the nearest comrade in front of him; a
  covering comrade with at least `FearAnchorFreshPercent` 60 % of his bar ANCHORS him - he holds his spot `FearAnchorSeconds` 20 (once per cover;
  the comrade tiring below the line ends it); a place within 1 m of his slot leaves him to his own AI (small fear, no tug of war with the game's
  walk to the slot). Under a CHARGE (no slots) he is left to his AI within (1 − p) × `FearChargeLooseMetres` 12 of the shelter (at least 1.5 m)
  and pulled in beyond it + 2 m. THE LEVER is courage's: a fifth movement wish on step 16's input hook (`AiInputState.Flock` - his own move bits
  out, the vector toward his place, slower within 2.5 m; 0, 0 = he stands; he faces his AI's target and strikes what reaches him); the ground
  ahead checked every 0.25 s. WHO STEERS: a step back's backpedal, then the wall's pull (step 63), then COURAGE'S WALK BACK, then the flock; a
  fever press (armed / burning and not shocked - with courage off too) or a surge ends a spell. THE SPRINT BACK (courage's regroup sprint, what he
  holds above `FearSprintKeepPercent` 50 %, at most every 10 s): farther than `FearSprintMetres` 8 from his place, ALONE (fewer than 2 mates in
  courage's circle with an enemy in it), or a `FearSprintChancePercent` 10 % roll a look beyond 3 m. WALLS: a shield wall / square / circle keeps
  its men while its men's mean morale is at least `FearWallBreakMorale` 30 - below it step 41's held men are let go (`WallVerdict.Breaking`) and
  the frightened flock. Never the player, riders, busy / detached / routing men, a siege's attackers, no formation, a retreat order; no
  tournaments, arenas, naval. Works without the Athletics system (no tiredness, no sprint) and with courage off. Master switch FIRST, then
  `FearFlockEnabled`: off = every spell let go at once (their formation places again); the looks still MEASURE - the summary's 7 "fear flock"
  lines compare the units' spread, depth ÷ width, drift back by order and the men covered ON vs OFF.
- **Winners don't hang back** (step 69, Toni's battles 2026-10-19 - AI_NOTES "Step 69", decision 62; the fear flock's `FearLookCore`, Core
  `FearMath.Win` / `IdleMen.cs`): the fear flock's fear came from morale and tiredness only, never from who is winning - a tired green WINNER kept
  his shelter behind his unit's centre. Now, at the same look: his WIN = 0 below `FearWinEvenOddsPercent` 100 (courage's toughness-weighted
  local odds - losing: every step-67 number kept), else 1 − (1 − w)(1 − `FearWinShakenPercent` 50 % × s), w = clamp((odds − 1) ÷
  (`FearWinClearOddsPercent` 200 % − 1)), s = the enemy near him SHAKEN (the toughness-weighted mean of their morale shares on `FearMoraleCalm` /
  `FearMoraleBroken`; a routing man's morale is spent); no enemy in courage's radius = his unit's mean win (its men in contact); with one, the
  better of his own and (1 − u) his own + u the unit's (`FearUnitSharePercent`). His fear × (1 − win) - the unit's table still feeds on the
  fear before the win, its mean pull (the shelter) on the pull after it. NO PULL WITHOUT A THREAT: no enemy within `FearThreatMetres` 12 m
  (courage's scan; beyond its 8 m one enemy read, only for an afraid man) = no pull - he follows his order; the morale and bars still show.
  THE MOP-UP: a CHARGING unit whose men in contact win `FearMopUpWinPercent` 50 %+ on average drops the loose-mass leash (no anchor, no
  stepping in) - his AI's charge drives him. Spells end "winning" / "no enemy near". THE IDLE-MEN DIAGNOSTIC (measurement only, ON and OFF):
  an AI man on foot whose unit charges / advances (a wall's advance too), an enemy within 25 m and none within 3 m, standing (below 0.5 m/s
  between two looks) more than 5 s - the man-seconds and men by who held him: a ranged weapon in hand / a ranged or skirmisher formation
  (NORMAL, apart), the wall hold, the fear flock's steering, courage's timidity, the brace, else the game itself; the [summary] "idle men" line
  and the first of each in full.
- **The flock is a wide band** (step 70, Toni's 30 v 50 looters 2026-10-19 15:59 - AI_NOTES "Step 70", decision 63; Core `FearBand.cs`, the
  flock's `FearLookCore`): the shelter was a POINT, so the frightened unit drew into a column. Now each unit's BAND, published every 0.5 s:
  its direction = toward the enemy its men in contact see (the enemy formation's centre when none; aligned with that formation's own facing
  when it faces the unit within 120°; else the unit's facing); its width = max(the enemy formation's frontage, men ÷ ranks ×
  `FearBandFileMetres` 1 m), at most `FearBandMaxWidthMetres` 40, along the enemy's front; its depth = ranks × `FearBandRankMetres` 1, ranks =
  `FearBandRanksMin` 4 at no pull to `FearBandRanksMax` 6 at the full pull (the unit's mean, rounded); its FRONT wants `FearBandGapMetres` 1.5 m
  short of the enemy's front (the men's own front when none) − `FearShelterBackMetres` 4 × the pull, moving forward freely and back at most step
  67's drift cap. HIS PLACE: his lateral (his slot's - under a charge his own - clamped into the band) KEPT; his depth compressed only (ahead
  of the band = its front, behind it = his rank's place: front − (rank − 0.5) × the rank); the files counted at each publish by where each man
  is BOUND (his position, a slid man's file), his rank = 1 + the men ahead of him in his file; in a file holding more men than the ranks the
  men behind them SLIDE to the nearest file with room (toward the ends on a tie), reserved at once, kept for the spell (moving on only if
  that file overfills behind him); a hold blends slot → band by the pull (a slid man goes the whole way along the front); a charge targets the
  band place, loose while within (1 − p) × 12 m of it in depth and 1.5 m along the front; "step in behind a comrade" only where his file has
  room. FRONT MEN STRIKE: a flocking man nobody covers with an enemy within `FearStrikeReachMetres` 2.5 is let go (his AI steps in and
  swings) unless more than `FearStrikeLeashMetres` 2 ahead of his place (then walked back). `FearBandEnabled` off = step 67's point. The
  [summary] "fear flock band" line: in contact, men per occupied 1 m file (the depth in RANKS) and the deepest file, the width (occupied files),
  mean and p90, ON vs OFF, the files over the band's ranks; how the places were made; steps in kept / refused; strikes and leashes.
- **No lone runner behind the enemy's line** (step 70, decision 64; Core `LoneRunner.cs`, `AthleticsLogic.LoneRunner.cs`): a LONE RUNNER = an AI
  man on foot in a formation (courage's gate - never you, never riders), fewer than 2 mates in courage's circle with an enemy within 8 m,
  more than `LoneRunnerAheadMetres` 4 ahead of his own unit's front (its men's mean + √3 σ along the band's direction) or 1 m past the enemy's
  front. Measured at every courage look ON and OFF by cause, the first that holds: the sprint into contact, the punish step-in, a courage surge,
  the fever press, chasing a target (`GetTargetAgent`, read for lone men only) behind the enemy's front, the game - and his target behind the
  front by whom (you / a hero / another). `LoneRunnerEnabled` (master switch FIRST): (1) the fever presses against bad odds, not for a man
  ALONE - courage's odds and walk decide him; (2) a charge's / fever's sprint INTO contact ends (`SprintEnd.LoneRunner`, checked every 0.25 s)
  when the last look said alone or he stands past the enemy's front; (3) a lone runner walks back to his group - courage's walk whatever the
  odds, its side round the enemy chosen once, never turned round halfway by his fever, a surge or a punish step-in - unless he chases a
  BEATEN enemy (his side's odds at `FearWinClearOddsPercent` and the enemy near him shaken 0.5+). The [summary] "lone runners" line.
- **Cavalry flocking** (step 55, AI_NOTES "Step 55", decision 47; `Missions/AthleticsLogic.Flock.cs`, `Missions/FlockBody.cs`, Core
  `CavalryFlock.cs`; Anton 2026-10-18: "fresh and eager riders do the vanilla dance ... the more tired a rider gets, the more he goes to meet
  others of his own kind ... the flock is never a mass sitting somewhere ... it FLIPS from flocking up to going after its target as a flock"):
  every AI rider whose formation's order is a CHARGE (charge / charge to target - hold, move, follow and advance keep riders in slots), both
  sides, never the player, never a busy / detached / routing rider or one the game scripts itself, no tournaments / arenas / naval, is in a
  CENSUS every 0.5 s. His KIND is read once from his kit (a bow / crossbow / sling with ammo = horse archer; throwing weapons left = skirmisher;
  else melee); his FLOCK = (team, formation, kind). His EAGERNESS e = his bar (points ÷ FULL pool - a wound counts through the cap; 1 with
  Athletics off) × 0.5 when courage's odds within `CavalryFlockTimidRadiusMetres` say he is alone among enemies (`CavalryFlockTimid`, courage
  on). e ≥ `CavalryFlockFreeEagerPercent` (75) = FREE: vanilla's dance, untouched. Below it he FLOCKS: the game lets us script him
  (`CanBeAssignedForScriptedMovement`), his horseback weights lowered (`CavalryFlockTightenWeights`); his GROUP = the flocking riders of his
  flock within `CavalryFlockGatherRadiusMetres` (25 m, him included); its centre, mean velocity, mean eagerness, LEADER (its most eager rider);
  its HEADING = Σ readiness × the unit vector to the formation's GOAL (the charge-to-target order's target formation, else
  `Formation.TargetFormation`, else the game's cached closest enemy formation - its centre) + Σ (1 − readiness) × the unit vector to the
  unit's centre of mass (`CachedAveragePosition`), readiness = e ÷ the free line; he rides (`Agent.SetScriptedPosition`, re-sent when the point
  moved 2 m or after 2 s) to the centre + the heading × max(the group's speed, 4 m/s) × `CavalryFlockLeadSeconds` + his sunflower slot
  (`CavalryFlockSeparationMetres` apart). THE GROUP-SIZE CURVE: N(e) = 1 at the free line, `CavalryFlockMidGroupSize` (4) at
  `CavalryFlockMidEagerPercent` (40), `CavalryFlockExhaustedSharePercent` (65%) of his kind's riders at 0 (never below the middle size), lines
  between, rounded up, capped at his kind's flockers. THE FLIP: the group's count ≥ N(its MEAN e), its leader's readiness ≥ 0.5 and he has
  flocked ≥ 3 s → every flocking rider of the group released at once (the scripted walk off, the game's weights back) - a WAVE that charges as
  the game's own riders for `CavalryFlockWaveSeconds` (10), then each decides again from his eagerness. A flocker free again at free +
  `CavalryFlockHysteresisPercent`; released after `CavalryFlockMaxSeconds` (45) without a flip. Master switch FIRST, then `CavalryFlockEnabled`:
  off = every flocker let go at once; the census still MEASURES. The `[summary]` "cavalry" lines: the share of rider time FREE / FLOCKING / IN A
  WAVE, flips a minute and wave size by the flock's mean eagerness (small fresh, big tired) + the leader's eagerness, the TIME BETWEEN PASSES per
  rider by eagerness band ON vs OFF, LONE PASSES (a pass with fewer than 3 of his kind within 30 m) ON vs OFF, the spread, the lever, the cost.
- **Impact - staggers, dropped shields, knockdowns** (IMPACT I1-I7 = steps 47-53, the whole design in `docs/IMPACT.md`; step 48 =
  I2, THE MODEL, decision 40): every kick / bash that lands, every block (right side, parry, wrong side) and every clean weapon hit on
  a man we track is decided ONCE by Core `ImpactMath.Decide`: the pressure x on the man who takes it = his tiredness T = 1 − f +
  `ImpactMismatchWeight` (0.25) × the mismatch beyond the even band (± `ImpactEvenBandDoublings` 0.5) - R = (both men's CURRENT
  points, never below `ImpactBodyFloorPoints`)^`ImpactBodyExponent` × (the blow's impact × the attacker's skill factor) ÷ (the guard ×
  the defender's skill factor) - + `ImpactStaggeredBonus` (0.75) while he reels from an impact stagger + `ImpactDamageWeight` (0) ×
  the landed damage ÷ his full health; each outcome = its ceiling (the `*MaxPercent` table, IMPACT §3.5) × S((x − from) ÷ (1 − from))
  × `ImpactDiminishFactor`^(his outcomes in the last `ImpactDiminishWindowSeconds`) (× `ImpactOnPlayerPercent` against you; a rider:
  never a stagger, a knockdown = knocked off at `ImpactRiderDismountPercent`), one die each, knockdown → shield → stagger (the
  knockdown replaces the stagger); the riposte (a right-side block or parry) with the roles turned × the defender's f; lengths = base ×
  (1 + extra × T) × a roll in [1 − r T, 1 + r T]; immunities (`ImpactStaggerImmunitySeconds` after a stagger ends,
  `ImpactKnockdownImmunitySeconds` after getting up) kept on each man's record (Core `ImpactState`). The inputs (step 48): both men's
  Athletics from our records (the attacker's swing × step 20b's animation share), the weapon in the collision's slot (item weight, the
  modified swing / thrust speed by the strike type, the collision's damage type, the attacker's relevant skill; a bash = the shield's
  weight and one-handed; a kick = Athletics), the defender's guard (the off-hand shield and one-handed, or the parrying weapon and its
  skill; no guard = Athletics), mounts, health - `Missions/ImpactInputs.cs`. A landed blow on a man on foot is decided in the damage
  decorator's KnockDown seam (before the engine registers it) and taken by OnMeleeHit; blocks in OnMeleeHit. Step 48 decided and
  LOGGED only; each effect step adds its family to `ImpactMath.EffectsBuilt` - **step 50 (I3): kicks and bashes are LIVE** (below),
  **step 51 (I4): blocks + the riposte are LIVE** (below), **step 52 (I5): clean hits + MOMENTUM are LIVE** (below - every family live now;
  the board moved by one when step 49 was inserted) - and applies the same decision through the
  spike's seams; a family decided live whose effect is not built yet decides on COPIES of the men's records (`ImpactRules.KeepsState` -
  a stagger that never happened gives no reeling bonus, immunity, halving or courage shock). Each family behind its own switch (`ImpactKicksEnabled` / `ImpactBlocksEnabled` /
  `ImpactHitsEnabled` / `ImpactRiposteEnabled`) and `ImpactShadowMode` turning ALL of them into logging only (the spike forces shadow
  while it runs). Master switch FIRST, then `AthleticsEnabled`, then `ImpactEnabled` - read live at every event. The summary's "impact"
  lines count every decision by event × the defender's bar band (100-75 / 75-50 / 50-25 / 25-0 %) - asked, mean x, rolled, mean chance,
  would apply, the refusals (on the ground, immunities, no shield, a rider), the halvings, the lengths asked, the cost.
- **Impact I3: kicks and bashes LIVE** (step 50, AI_NOTES "Step 50", decision 42; `Missions/AthleticsLogic.ImpactLive.cs`, Core
  `ImpactLive.cs`): a landed kick / bash's decision is APPLIED - the **knockdown** = the damage decorator's KnockDown answer (vanilla never
  knocks down with a kick); the **shield drop** = queued in the hit callback and made from the NEXT TICK (`IImpactBody.DropShield` - the
  vanilla queued DropItem; never while he is in his own release / bash - vanilla's drop rule - waiting up to 1 s), the shield stays on the
  ground, pickable by vanilla's rules (IMPACT §8 answer 3) - an AI man may not pick a shield up for `ShieldPickupLockSeconds` (0 = never
  locked) through a decorator of the game's `ItemPickupModel` (only the AI asks it; your use key is never locked); **the longer reel** =
  his knock-back reaction at speed 1 ÷ `ReelFactor` (x1 fresh → up to x2.5 x (1 ± 30%) empty) the tick it shows and again on each
  _continue; **the slower get-up** = his fall-and-rise at 1 ÷ `GetUpFactor` (up to x2) - both MEASURED by Core `ReactionWatch` and their
  real ends written into his record (`ImpactState.EndReel` / `GotUp`), so the 2 s stagger immunity and the 5 s knockdown immunity run from
  what the engine played (the step-48 estimates 0.45 s / 1.5 s only until the watch ends); the 8 s halving as decided. Never a rider
  (knocked off = step 53: decided, nothing applied, his record untouched). You: the same rules × `ImpactOnPlayerPercent`; every kick you
  deal or take is an `[impact] YOU:` line (what it did with its chance, or why nothing). Courage (step 49): a live outcome is SHOCK (his
  record's recent outcomes, `CourageShockSeconds`). Each lever (the KnockDown answer, the drop, the slowed reaction, the watch) is disabled
  ALONE for the mission on an error (one `[error]`); at most 20 drops a tick (plumbing), 64 watches / 32 shields (pools). Off / shadow /
  `ImpactKicksEnabled` off / the master switch = nothing answered, every queued drop and watch let go at once (a slowed reaction ends by
  itself). The `[summary] impact live` lines: knocked down / shield dropped decided → applied → seen by the kicked man's bar band (and why
  not), the reel and the get-up asked vs measured (mean, max; untouched reels as the control), shields dropped → picked up again (after how
  long) / still on the ground / pickups locked, the limits that refused or halved a live kick, the levers disabled, the tick's cost.
- **Impact I4: blocks and the riposte LIVE** (step 51, AI_NOTES "Step 51", decision 43; `Missions/AthleticsLogic.ImpactBlocks.cs`, Core
  `ImpactBlocks.cs`): a block's ONE decision (right-side shield, parry, WRONG-side shield - IMPACT §3.5's rows) is APPLIED - **the stun by
  tiredness** = the damage decorator's stun seam (`CalculateDefendedBlowStunMultipliers`, WORKER threads, read only): the blocker's stun x
  (1 + `BlockStunTiredExtraPercent` × T) - no die, so the decision logs the very number the seam applies; T from a per-man snapshot the
  main thread writes every tick (x `ImpactOnPlayerPercent` for you, 0 on a horse); the game's own stun held at the game's 0.8 s cap first,
  ours at most `ImpactStunCapSeconds` - the game's `StunPeriodMax` raised to it while blocks are live and given back when they stop; every
  stun we do not lengthen keeps the game's 0.8; the attacker's recoil is the game's (his longer recoil is the riposte); **the rolled
  stagger** = `act_stagger_backward` on channel 0 from the NEXT TICK (`IImpactBody.ForceStagger` - never inside the callback; CanForce first:
  never a rider, a horse, a man on a siege engine / ladder, in water or already down - no engine call), asked with the engine's priority,
  then over it; at the speed that gives IMPACT §3.7's length (0.6 s fresh → 1.5 s ± 30% empty, at most `StaggerMaxSeconds`) - the
  animation's own length LEARNED from the staggers measured (0.9 s guessed until one is); WATCHED, its measured end = his reel's end (the
  2 s stagger immunity from it); **a wrong-side shield drop** = step 50's queued drop from the tick; **a blocker knocked down** = a blow of our
  own from the tick (vanilla's drowning-blow recipe, KnockDown | KnockBack, 1 damage, owned by the attacker - the spike's lever 6), its Fall
  watched and slowed by the get-up factor (as a kick's), the 5 s immunity from the measured get-up; **the riposte** (a fresh blocker's
  right-side block or parry, IMPACT §3.6, at most 50%; step 56 - decision 48: any blocker's, up to 70%, driven by the attacker's T and the
  turned mismatch, while the correct blocker himself counts a quarter of his T at 30 / 10, and the stun's lengthening is off) = a forced stagger of the ATTACKER from the tick, its length the decision's recoil
  (`RiposteStunMaxSeconds` x (0.5 + 0.5 T_A) ± the spread), watched the same way. `ImpactBlocksEnabled` off with `ImpactRiposteEnabled` on =
  the riposte alone. An outcome the dice gave that is not played (its lever off, the pool full, refused at the tick, the engine refusing)
  leaves no reeling / ground time on his record. Courage: each live outcome is ONE shock (our own blow's hit is not a second). You: the same
  rules x `ImpactOnPlayerPercent`; every live block outcome you deal or take is an `[impact] YOU:` line ("your right-side block of X's
  Sturgian Axe: you are staggered (55%)", "X blocked you (parry): you reel (riposte, 50%)"). Each lever (`BlockStun` - its worker-thread
  error picked up by the tick -, `ForcedStagger`, `OwnBlow`) disabled ALONE on an error; off / shadow / the spike / `ImpactBlocksEnabled`
  off / the master switch = the game's stun at once (the seam reads the switches live), the cap back at the tick, every queued action and
  watch let go. The `[summary] impact live` block lines: per block kind x the blocker's bar band, staggered / shield dropped / knocked down
  decided → applied → seen (and why not); the staggers asked vs MEASURED (the speed, accepted with / over the engine's priority) and BLOCKS
  MADE FROM INSIDE A STAGGER (~0 = the window is real); the blockers knocked down (our blows → Fall seen, the get-up measured); the
  riposte by the ATTACKER's bar band; the stun - the seam's calls (off the main thread), lengthened (x, before → after, at the cap), the
  guard measured asked vs fresh blockers, the cap raised / restored; the limits, YOU, shock, levers, the queue.
- **Impact I5: clean hits LIVE, the reeling bonus, MOMENTUM** (step 52, AI_NOTES "Step 52", decision 44; `Missions/AthleticsLogic.ImpactHits.cs`,
  Core `ImpactHits.cs`; Anton's points 8 / 9 + his step-52 wish: "a fresh shock unit attacking a tired line must be devastating"): a clean
  hit's ONE decision (IMPACT §3.5's clean-hit row: stagger 80 %, down 50 % at the ceiling) is APPLIED - **the knockdown** = the damage
  decorator's KnockDown answer (as a kick's), its fall-and-rise watched and slowed by the get-up factor, the 5 s immunity from the measured
  get-up; **the stagger** = the engine's OWN flinch slowed (not a forced stagger) to the decided length (0.6 s fresh → 1.5 s ± 30 % empty):
  speed = the flinch's own length ÷ the asked length, the own length LEARNED from the flinches measured (untouched controls - at most 8 timed
  at once - and the slowed ones × their speed; 0.45 s guessed until one is); its measured end = his reel's end (the reeling bonus for the
  next blow, the 2 s immunity); **tired men never shrug off** (`ImpactTiredNeverShrugsOff`): when the game lets a man shrug a clean hit off,
  the decorator's `DecideAgentShrugOffBlow` (main thread - Mission.CreateMeleeBlow) asks again with the game's threshold × (1 − T) (T ×
  `ImpactOnPlayerPercent` for you; never a rider; clean hits only) - an exhausted man flinches at any damage. **The reeling bonus** (+0.75)
  is real on every family: a kick's lengthened reel, a block's stagger, a riposte and a clean hit's stagger all write the reeling time from
  what the engine played - a kick on a staggered man very likely knocks him down, a heavy blow can. **MOMENTUM** (every family - kicks, bashes,
  weapon blows, blocked or not): the attacker's own speed INTO the blow (his ground velocity, vanilla's formula, along the line to the man he
  strikes - `ImpactFacts.AttackerSpeed`) multiplies the blow's impact by 1 + `ImpactMomentumPercent` (60) % × clamp((v − walk) ÷ (sprint −
  walk), 0, 1), walk `ImpactMomentumWalkMetresPerSecond` 2.0, sprint `ImpactMomentumSprintMetresPerSecond` 7.0 (the game measured: a walk
  1.8 m/s, a run ~4.4, a fresh sprint ~8-9); never a rider's (step 53); the fever is not a factor of its own (its sprints are speed). The
  riposte reads it turned round (a charging attacker is harder to turn). Riders: nothing of a clean hit applies (step 53). You: the same
  rules × `ImpactOnPlayerPercent`; every live clean hit that did something to you or that you did is an `[impact] YOU:` line. Courage: each
  live outcome is ONE shock. Levers: the KnockDown answer, the slowed reaction and the watch shared with the kicks (each disabled alone);
  `ShrugOff` its own. The `[summary]` lines: **impact momentum** (every decided blow of every family by the attacker's speed band - standing
  < 0.5 m/s / walking < walk / running < sprint / sprinting: blows, mean speed, mean impact factor, an outcome %, knocked down % - all men
  and the men below half a bar: THE proof of the shock charge), the clean hits per weapon class (cut / pierce / blunt / two-handed) × the
  hit man's bar band (knocked down / staggered decided → applied → seen), knockdowns on REELING men vs the rest (and on blows the game had
  shrugged off), the staggers asked vs measured vs untouched flinches, the shrug-offs refused by band, the limits.
- **Impact I6: riders, the rider's momentum, the on-screen lines** (step 53, AI_NOTES "Step 53", decision 45; `Missions/AthleticsLogic.ImpactRiders.cs`,
  Core `ImpactRiders.cs`; IMPACT §3.8, §8 q4 / q12): **a rider's knockdown = KNOCKED OFF his horse** at `ImpactRiderDismountPercent` (50) % of
  the knockdown's chance (the model's `Dismount`), every family: a LANDED kick / bash / clean hit is answered in the damage decorator's
  `DecideAgentDismountedByBlow` (main thread - Mission.CreateMeleeBlow asks it INSTEAD of the KnockDown question for a man on a horse; OR-ed
  with vanilla's); a BLOCK (never registered by the engine) = a blow of our own flagged `CanDismount` (vanilla's own flag for it) from the
  next tick, owned by the attacker, 1 damage; each dismount followed until he is off the horse (seen) or 2 s pass (not seen: his record's
  ground time taken back). **Never a stagger or a slowed reaction on a rider** (the model refuses his stagger; no forced action on a mounted
  man); his **shield** can be knocked away (kicks / bashes, wrong-side blocks - the step-50 queue); his **block stun** x (1 + T) like anyone's;
  the riposte never staggers a mounted attacker; horses untouched (vanilla rears). **The rider's momentum**: his active blow (swing or
  thrust) carries his HORSE's speed into the blow - vanilla's own rider velocity (`MovementVelocity.y` × the mount's movement direction),
  along the line to the man he strikes - × (1 + `ImpactMomentumRiderPercent` (30) % × clamp((v − 3.0) ÷ (10.0 − 3.0), 0, 1)) (walk
  `ImpactMomentumRiderWalkMetresPerSecond` 3.0, gallop `ImpactMomentumRiderGallopMetresPerSecond` 10.0); a **couched lance** (a passive
  attack - `Agent.IsDoingPassiveAttack`) never (the game's own big hit). **The on-screen lines** (`ImpactShowMessages`, on): what was APPLIED to
  you (knocked down, knocked off your horse, your shield knocked away, reeling from a riposte - red #D65252, the game's "received" colour) and
  by you (a man knocked down / off his horse, his guard broken, his shield knocked away - purple #AE4AD9, the game's "delivered"); with
  `ImpactMessagesIncludeYourMen` also your side's notable moments (a man of yours fells / unhorses an enemy HERO, a hero of yours goes down -
  white); at most one line every `ImpactMessageMinSeconds` (1.5) s - what comes meanwhile merged into the next (3 parts, "(+N more)", the same
  part once); never in shadow mode. The `[summary]` lines: **impact riders** (decisions on riders by family, dismounts decided by the rider's bar
  → applied by way → seen / still mounted, why not), **impact riders' shields and limits**, **impact messages** (offered by role and kind →
  shown, merged); the momentum line's RIDERS columns by the horse's band (standing / walking / trotting-cantering / galloping) + couched lances.
- **Impact I7: the AI punishes a window** (step 54, AI_NOTES "Step 54", decision 46; `Missions/AthleticsLogic.ImpactPunish.cs`,
  `Missions/PunishBody.cs`, Core `ImpactPunish.cs`; IMPACT §7 I7, §8 q14 - Anton: "otherwise it's a player-only weapon"): a WINDOW = a man
  REELING or DOWN from a live impact outcome (his `ImpactState` - an impact stagger, a lengthened reel, a riposte's recoil, a knockdown, a
  dismount; the PLAYER's too) + `ImpactPunishGraceSeconds` (0.5). At the fever's look (every AI man ~every 0.25 s, staggered) an AI man ON FOOT
  IN CONTACT (an enemy within `FeverContactMetres`) reads his target (`GetTargetAgent()`, else `ImmediateEnemy` - one native read, made only
  while some window is open in the battle); a target in a window = he PUNISHES: his aggression share (step 43) is raised to
  `ImpactPunishAggressionPercent` (100 - the game's own eagerness; absolute, never lowered: S = max(his tired / fever share, P)) through the same
  recompute path (0.05 steps, the stat decorator writes the two decision chances) until a look sees the window closed; a man within
  `ImpactPunishRangeMetres` (3) of it gets courage's surge clock to the window's end - a TIMID man (step 49) steps in (never in a panic). Never
  the player as the attacker, never a rider (the decorator keeps riders' values), never in shadow / the spike. Off (`ImpactPunishEnabled`) =
  his own share at once; the windows are still measured. The `[summary]` lines: **impact punish windows** (opened on AI men / on you, seen by an
  AI enemy in contact, PUNISHED - an AI melee attack started inside -, wasted), **impact punish proof** (AI melee attacks per man-minute in
  contact against a target in a window vs not, the punish ON and OFF), **impact punish men** (raised, ended, timid men sent in, attacks in your
  windows, target reads, cost).
- **Tired fighters step back** (built in step 5d - the literal rule; `StepBackEnabled`, on):
  when a MELEE swing ends, an AI fighter on foot may step back, facing its enemy with its guard
  up. Chance = `StepBackMaxChancePercent` (100) × (1 − f), f after the swing's cost: 0% in the
  peak zone (no roll at all), 50% halfway down to 0, every swing at 0. He walks BACKWARDS
  `StepBackDistance` (2 m) straight away from the enemy he fights, facing him, guard up, making no
  swings (`StepBackHoldAttacks`), until he has covered the distance or `StepBackSeconds` (1.5 s,
  read live) run out - then his formation takes him back. Never the player, never riders, never
  after ranged attacks, kicks or bashes. The point: the tired fall back and the fresh take the
  blows.
  - **How** (step 16, `StepBackBackpedal`, on - AI_NOTES "Step 16"): a backpedal through the AI's own
    input - the same per-man component as the AI's pause writes a backwards movement into his
    controls, like a player holding S, along the line straight away from his enemy (turned into his
    own frame every tick, so he backs along the line the safety checks cleared whichever way his AI
    turns him); his AI keeps facing his enemy and keeps its guard (only the attack bits are taken
    out, and his own forward / sideways wishes, so only the backwards movement moves him). It stops
    at the distance covered ("arrived"), at the time, or when the ground a little further back stops
    being walkable ("edge ahead" - checked every 0.25 s: off the navmesh, a height step, no straight
    way - so nobody backs off a wall walk or into a ditch). Then we simply stop writing: his own AI
    and formation take him back.
  - **The old way** (`StepBackBackpedal` off, steps 5d-15): the engine's scripted movement
    (`SetScriptedPositionAndDirection`, released by `DisableScriptedMovement`) - how vanilla sends a
    soldier to pick up arrows. It is navigation: a man walks facing his path, so ~80% turned their
    backs mid-step and only 2 of 2159 arrived (the playtest of 2026-09-27; BATTLE_PACING §B).
    Research, the alternatives weighed and the fallbacks: AI_NOTES "Step 5d" and "Step 16".
  - **Only where it is safe** (Claude's calls, Anton can overturn): field battles only - not
    tournaments, arena fights, duels or naval battles; not while the formation stands in a
    shield wall, square or circle (they exist to hold) or is ordered to retreat; not for men the
    game is using (ladders, siege towers and engines, picking something up, routing); only with
    the enemy he fights within `StepBackEnemyRange` (4 m); only to a level spot on the navmesh
    with a straight way back (no wall edges, stairs, fences); at most `StepBackMaxAtOnce` (50) at
    once on the whole field.
  - **Always ends**: his time is up or (backpedal) the distance covered or the ground ending
    behind him; he falls or leaves; his formation gets a new order or
    arrangement; he mounts, routs, changes formation, or the player takes him; the battle ends;
    or the step back (Athletics, the whole mod) is switched off - then everyone stepping back
    walks back at once.
  - **His pause survives it**: a step back never cancels the no-attack pause above - the attacks
    stay held until the later of the two ends (step 16; until then a started step back dropped the
    pause, so an empty man attacked again 1.5 s later).
  - If the backpedal still shows men turning their backs, the next fallbacks are RBM's short
    position-lock hops (`Agent.SetTargetPosition`) or "hang back" through the AI's behaviour values
    (AI_NOTES "Step 5d", BATTLE_PACING §B).

## 2b. Athletics v2 (folded into §2)

Anton's additions of 2026-09-27 were written here while step 5 built the first version; step
5c built them and folded them into §2, which is now the one current spec. The step-back rule
(built in step 5d) is §2's last bullet.

## 2c. Defaults file (built in step 5b)

`defaults.json` at the repo root holds EVERY parameter's default, one key each, with the
plain-words explanation above it — the one place Anton tunes defaults, then pushes. **It is
the single truth for default values**: the build embeds it in `TraxCombat.Core.dll` and the
settings schema reads each default from it while it is built (the schema declares types,
ranges, groups and wording — no default values). So the first-run config file, a key missing
from config.json, MCM's Default preset (its Reset buttons), the "Revert all to defaults"
button and the "(default …)" in comments, MCM hints and the log all show the file's values.

- **MCM, group "Defaults"** (step 59: *Revert* on both pages and reverting both, *Save* on the Advanced page only):
  **Revert all to defaults** puts every setting back to its
  defaults.json value at once — live, even mid-battle; each change is logged
  `(source: defaults)` and config.json is rewritten. **Save current values as a defaults
  file** writes the values in play as `defaults.json`, comments and all, next to config.json
  (the path is logged and shown on screen) — copy it over the repo's to make a tuning found in
  game the new defaults.
- **Without MCM**: delete a key's line in config.json (it takes its default at the next battle
  start) or delete the file (everything does); the config file's header says so.
- **The Parameters table's Default column is the INITIAL value** — what the setting started
  with. It is NOT compared with the code or with defaults.json: Anton tunes defaults.json and
  never has to edit this document for it. The table must still list exactly the schema's keys,
  each with a value of the right type (the schema test).
- **Checks** (DefaultsFileTests): every schema key is in defaults.json and nothing else; each
  value has the right type (true/false, a whole number, a number) and lies inside its range;
  the // lines are exactly what the mod writes; the build embedded that very file. When a
  setting's wording, range or place changes, `dotnet run --project tools/DefaultsTool --
  refresh` rewrites every comment and keeps every value. The unit tests run on this table's
  INITIAL values, so a tuned default never breaks them; the offline smoke checks that the real
  DLL read every default from the embedded file.
- **A new setting**: its row in the table below, its schema entry, `"Key": value` anywhere in
  defaults.json, then the refresh command puts it in place with its comments.

## 3. Showing Athletics

All toggles, all on by default. **Built: items 1, 1b, 1c, 3 (step 20, in its "hold ALT" form), 3b (step 44), 3c (step 46) and 4. Item 2
is LATER** (Anton moved it off the build order, 2026-09-27): its design stays here, its settings wait in
"Planned parameters" below, and nothing of it is in the game, MCM or the config file.

1. **Player bar** (`ShowPlayerBar`): the player's Athletics bar near the vanilla health bar,
   in the spirit of RBM's posture bar (RBM = Realistic Battle Mod, confirmed by Anton —
   style reference only). Built in step 6.
1b. **Attack recovery bar** (`ShowAttackRecoveryBar`, Anton 2026-09-27: "above that bar add a bar
   'attack recovery' that empties when I attack and until it fills I can't attack; inside it add
   the secs delay added"): your no-attack pause (§2) made visible, just above the Athletics bar.
   Built in step 13 — below.
1c. **Sprint bar** (`ShowSprintBar`, step 33): your sprint store (§2 "The sprint") as a slim row above the Attack
   recovery bar (`SprintBarOffsetAbove` 46 px over your bar's row; as long and thick as the recovery bar): the words
   "Sprint 6.2 s" (the seconds stored, tenths rounded down), the fill = the store's share of its size now, a white
   mark at `SprintMinStartPercent` (hidden at 0); grey below the mark, blue when a sprint can start, yellow while you
   sprint. Shown in fights with your Athletics bar (`ShowPlayerBar`) and the sprint (`SprintEnabled`). Outside a battle
   (walking about a town or village - Anton 2026-10-14: the sprint works there too, so street fights have it) by the
   Athletics bar's own rule (`ShowPlayerBarOutsideBattles`): a weapon drawn, or your Athletics or sprint store below
   full, then the grace.
2. **LATER (step 7) — Looked-at NPC** (`ShowTargetBar`): a small bar for the fighter the player
   is aiming at / looking at, within `TargetBarMaxDistance`; it lingers `TargetBarLingerSeconds`
   after the aim leaves so it does not flicker. Aiming at a horse shows its rider.
3. **Hold ALT — numbers under the formation markers** (`ShowAltMarkerStats`; built in step 20, Anton
   2026-09-28: "I see the Athletics and health numbers above the troops when I hold ALT — it now shows
   me the troop count and distance"). This is step 8's "squad bars" in their "only while vanilla shows
   its formation markers" form. Built as below; an always-on form (`FormationBarsAlways`,
   `FormationBarHeight`) stays in "Planned parameters".
3b. **Hold ALT — fever marks above the men** (`ShowFeverMarks`; built in step 44, Anton 2026-10-17: when I
   hold ALT the game shows a small icon above each of my soldiers - "see there which soldiers are in FEVER"):
   a small bar of ours above each man in a fever (§2 "Aggression and the fever"), shown with item 3. Built as
   below.
4. **Orders menu** (`ShowInOrderMenu`): while the orders menu is open, a strip directly UNDER
   each of vanilla's formation cards (built in step 9 — below). Numbers INSIDE vanilla's cards
   would need UIExtenderEx and risk clashing with RTS Camera Command System — not worth it
   (Claude's call 2026-09-27, see RESEARCH implication 1).

**The orders-menu strip as built (step 9):** one cell under each card the game draws, as wide as
the card. Just under the card, left: the men's average Athletics as a share of their own pools
and its spread, `72% ± 8` (the ± is `FormationSpreadStdDevs` standard deviations, in points of
the bar); right: their average health left, `HP 81%` (`ShowFormationHealth`; the game's font has
no heart). The middle stays free for the game's own order icons, which hang 20 px under a card.
Under those, a slim bar (`OrderStripBarHeight`, 4 px): the average fill coloured by the men's
average f with the player bar's colours (green … red), a lighter band from mean − k·σ to
mean + k·σ (`ShowFormationSpread`), a thin tick at the peak line. The player himself is not
counted (the cards count the men under his command; he has his own bar). Refreshed every
`FormationStatsRefreshSeconds`.
- **How it lines up** (no UIExtenderEx, nothing patched): the game's own cards are READ live —
  the order layer's widget tree is walked once per open, and every frame each drawn card's
  screen position and size are read and the cell is placed on its bottom edge in pixels. So it
  fits any resolution, UI scale, the keyboard columns and the gamepad row, and RTS Camera
  Command System's reworked cards (its columns run bottom to top; the same reader works). Card
  slot k is formation k (Infantry, Archers, Cavalry, …); each card's own man-count confirms it
  every frame. A cell that would leave the screen is lifted to its edge.
- **Fallback — a compact panel** at the top centre (`OrderPanelOffsetTop`, `OrderPanelWidth`), one
  row per formation ("1 Infantry  72% ± 8  HP 81%" and the same bar), when the cards cannot be
  trusted for that open (none found, not whole sets of 8, two sets at once, none drawn 0.5 s
  after the open, a card disagreeing with its formation for 1 s) or `OrderStripUnderCards` is off.
  The next open tries the cards again. Every fallback is logged with its reason.
- Five Advanced settings place the cell (`OrderStripTextSize`, `OrderStripTextOffset`,
  `OrderStripBarOffset`, `OrderStripBarHeight`, `OrderStripSideMargin`), all live. The defaults
  make the cell 23 px deep, which fits the bottom card of a column at 1920 × 1080 exactly.
- **The ready count** (step 24, `ShowReadyCount`, Anton 2026-09-29: "some indication above the squad of
  ready men, that are not resting in their current order status"): a third row under the bar, left,
  `ready 34/50` — the men NOT bracing (§2 "Brace by orders") out of the men the card counts (you are
  not counted); plain while most are ready, **yellow** at or below `ReadyYellowBelowPercent` (75) %
  ready, **red** at or below `ReadyRedBelowPercent` (50) %. The panel rows carry it after the health.
  Hidden while bracing is off (`BraceEnabled`, `AthleticsEnabled`, the master switch). Its row sits at
  `OrderStripReadyOffset` (24 px, right under the bar): the cell is then 39 px deep, filling the 40 px
  gap between two stacked cards; the bottom card of a column at 1920 × 1080 (24 px to the edge) gets
  its cell lifted 16 px — only while the row is drawn. Values at the strip's usual refresh, all live.

**The numbers under the formation markers as built (step 20 — hold ALT):** vanilla draws a marker
above every formation (the troop count, the formation icon, the distance) while you **hold the
"show indicators" key (Left Alt; the controller's LB)** or **the orders menu is open** — its own rule,
the same for every side. Exactly while vanilla shows them, each marked formation also gets, centred
just under its marker (`AltMarkerOffset`, 2 px under the distance number):
- `72% ± 8` — the men's average Athletics as a share of their own pools ± `FormationSpreadStdDevs`
  standard deviations (`ShowFormationSpread`), **in the colour of the men's average f** (the player
  bar's bands: green … red) — and `HP 81%`, their average health (`ShowFormationHealth`);
- under them a slim bar (`AltMarkerBarWidth` × `AltMarkerBarHeight`, 60 × 3 px; 0 = none): the mean
  fill in that colour, the lighter ± band, the peak tick — the strip's bar.
- under the bar (step 24, `ShowReadyCount`): `ready 34/50` — the men not bracing, the strip's count and
  colours (plain / yellow / red), on every side's labels (the enemy braces too); hidden while bracing
  is off. In your own formation you are not counted, so it may read one fewer than vanilla's count.
- **Which formations**: every one vanilla marks — yours and your allies' always, the **enemy's** too
  (`AltMarkersShowEnemy`, on: knowing the enemy is tired is the tactical point). The player himself is
  not counted in his own formation (he has his own bar); everyone else is, on every side. A formation
  with no tracked man gets no numbers.
- **How it lines up** (nothing patched): vanilla's marker layer is READ LIVE — its own "markers shown"
  flag (so ALT, the orders menu, or any mod's change to that rule is followed exactly), each marker's
  screen point (the formation's median + 3 m, projected by the game THIS frame) and each marker
  widget's live size. The label goes to the marker's bottom centre, computed exactly as vanilla
  centres the marker, the same frame — at any resolution, UI scale, and under RTS Camera's free camera
  (it moves the camera vanilla projects from). A marker vanilla hides — behind the camera, faded
  (closer than 5 m), its formation empty or gone — has no label; a targeted enemy's marker vanilla pins
  to the screen's edge keeps its label under it.
- **Fallbacks**, each logged with its reason: the marker widgets not found → the game's points with a
  nominal marker size; no marker layer / data at all → our own projection of the same point with the
  game's rule copied (ALT or the orders menu). The next show tries the live markers again.
- Values every `FormationStatsRefreshSeconds` (the averages are kept for every formation of every
  side); the positions every frame. Shown only in fights, with the master switch, Athletics and
  `ShowAltMarkerStats` on, never with the game's Hide battle UI or photo mode; the player need not be
  on the field (vanilla shows its markers after your fall too). All live.

**The fever marks as built (step 44 — hold ALT):** while you hold ALT the game also draws a small banner
icon above each of YOUR men (vanilla's `MissionAgentLabelView`: a 3D mesh on the man's body, yours and your
allies' only). Exactly while the formation markers above show (the same live flag - ALT held or the orders
menu open), each AI man in a FEVER - armed by a new charge / advance order, or burning after his first
contact - gets a small bar of ours above his head:
- **The look**: a slim bar (`FeverMarkWidth` × `FeverMarkHeight`, 22 × 5 UI px, the game's UI scale applies)
  with a dark 1-px frame; its FILL is the fever left (full while armed). **Bright orange** while armed and at
  the burn's start, through **yellow** as the store empties, its alpha falling from 1 to 0.45 with it; gone
  the moment the fever ends (faded, a hold / move / retreat order, the man falls). A bar, not a dot: it shows
  the state (colour) AND how much surge is left (length) at a glance, from the one native sprite every Trax
  bar uses.
- **Where**: centred above the man, its bottom edge `FeverMarkHeightMetres` (2.5 m) above his feet - just over
  vanilla's icon (2 m) - projected through the combat camera every frame, so it rides the man (and follows
  RTS Camera's free camera). Vanilla's own fade of its icon: none closer than 1.5 m, fading in up to 8 m;
  full size to 20 m, a straight line to 60% at 60 m and beyond.
- **Who**: AI men on foot in a fever - yours and your allies' always, the enemy's too (`FeverMarksShowEnemy`,
  on: you see their charge coming and when its surge is spent - vanilla's icon never marks the enemy, ours
  does). Calm men, riders and you (never a fever): nothing.
- **How many**: at most `FeverMarkMax` (200) a frame - every man in a fever in front of the camera is
  projected and the ones off the screen dropped first, then the NEAREST 200 drawn; the rest wait. A fixed,
  reused pool of marks: nothing allocated per frame (1000 men, 400 in a fever: ~0.03 ms a frame in the smoke,
  plus the game's own projection per man).
- **Never vanilla's icon**: its mesh list is private and vanilla rewrites the mesh's colour arguments on every
  highlight change - a tint of ours would fight it. Ours is a separate 2D layer; nothing of vanilla's is touched.
- Shown only in fights, with the master switch, Athletics, `FeverEnabled` and `ShowFeverMarks` on, never with
  Hide battle UI or photo mode; the player need not be on the field. All live.

**3c. The Athletics bar above every man as built (step 46 — hold ALT; Anton 2026-10-17: "add how tired they are in a bar
above that fever bar, maybe a green bar that gets to red, like on the unit selection / formation screen"):** the same
view draws, at the same moments (the formation markers' live flag - ALT or the orders menu), a small Athletics bar
above EVERY soldier's head - the fever bar of 3b stays under it while he is in a fever:
- **The look**: `AthleticsMarkWidth` × `AthleticsMarkHeight` (22 × 5 UI px), a dark 1-px frame; the FILL = his points ÷
  his FULL pool (the strip's and the player bar's reading), the part his wounds hold dark red (the player bar's way); the
  colour = the bar's bands (`Bar*BelowPercent`, read live: green at or above the peak line, blue just below, yellow /
  orange / red, empty red). No peak tick (too small to read - the colour carries it).
- **The stack**: the box's bottom on his point (`FeverMarkHeightMetres` above his feet, a rider 1 m higher); the fever bar
  at the bottom, `AthleticsMarkGap` (2 px), the Athletics bar on top. The fever bar's PLACE is kept while he has no fever
  (whenever fever bars can show for his side), so his bar never jumps when a fever starts or ends; with the fever bars off
  for his side (`ShowFeverMarks`, `FeverEnabled`, the enemy's switch) the bar sits on the point itself.
- **Who**: every man the logic tracks - AI men on foot and riders, heroes and lords too; yours and your allies' always, the
  enemy's with `AthleticsMarksShowEnemy` (on). Never you (your own bar, bottom right).
- **How many**: ONE cap for both bars, `FeverMarkMax` (200 men marked a frame - the key kept: no config migration). The
  nearest ON THE SCREEN: the candidates (every man in front of the camera, at least 1.5 m away) are sorted by distance when
  the cap bites and projected nearest first until it is full - a man off the screen never takes a slot, and only about the
  cap's worth of projections are made (smoke: 1000 men all on the screen, 0.07 ms a frame, 0 bytes, 200 projections).
- Vanilla's fade and the smaller size far away as 3b. Shown with the master switch, Athletics and `ShowAthleticsMarks` on
  (`ShowFeverMarks` off and the fever off do not hide it); the layer goes only when there is nothing to draw. All live.

**3d. The ALT view decluttered (step 58, decision 50 - Anton 2026-10-18: "this ALT view is too cluttered. Maybe add those lines as some averages on
the group, and only show them on up to 5 soldiers that I'm directly looking at and are really close, making it work in tournaments too"):**
- **The near view** (the default; `AltMarksEveryone` off): 3b's and 3c's bars only on the men you LOOK AT - the candidates (in front of the camera,
  1.5 m to `AltNearMarksMetres` 15 m from the camera, their side switched on) ranked by the angle from the camera's forward (= nearest the screen's
  centre), the first `AltNearMarksMax` (5) on the screen drawn (`FeverMarkMax` the ceiling); the rest are never projected. No distance fade. The
  enemy switches as before. `AltMarksEveryone` on = 3c exactly.
- **The group on the label**: under `ready 34/50`, `fever 12/40` - the formation's men in a fever of its men, the fever bar's orange, shown while at
  least one surges (`ShowFeverOnLabels`), on every side the labels show.
- **Tournaments, arenas, duels** (no formation markers): the view shows while the game's ALT key is held (the copied rule, step 20) - the near bars
  on your opponent; men with no valid team (the arena's crowd) are never marked, in any view.

**3e. The morale eyes (step 67, decision 60 - Anton 2026-10-19: "give me eyes on their morale too: ALT to see the average morale of the whole group,
and the same 5 closest guys' morale too"):**
- **A morale bar on the men you look at** (`ShowMoraleMarks`): under ALT, at the BOTTOM of each marked man's stack (3d's near view - the same up to 5
  men; 3c's every-man view too), a third bar the Athletics bar's size (`AthleticsMarkWidth` × `AthleticsMarkHeight`, `AthleticsMarkGap` above it),
  a dark frame and a clear BLUE fill (#4C9BFF) = his morale ÷ 100 (the game's `Agent.GetMorale()`, 0-100, read at every formation-stats refresh -
  also for men in no formation); the fever bar's place and the Athletics bar stand on it. No morale known (the game keeps none - e.g. a tournament)
  = no bar, its place kept. The enemy's follow `AthleticsMarksShowEnemy`. With only the morale bars on the men are still marked. The [summary] "ALT
  bars" line adds "morale bars a frame (step 67)".
- **The group's morale on the near labels** (`ShowMoraleOnLabels`): under `fever 12/40`, `morale 64, fresh 41%` in a lighter blue (#8CC0FF) - its
  men's mean morale and mean Athletics (the label bar's fill) - only on the nearest `MoraleLabelMax` (4) formations whose marker distance is at most
  `MoraleLabelMetres` (60 m), re-picked every frame (at most 64 markers, nothing allocated). The [summary] "morale line" line.

Bars show in fights (battle, duel, tournament and stealth modes — step 6 added stealth:
a stealth mission's fights cost Athletics too), with the player on the field, never while the
game's "hide battle UI" or photo mode is on, and never while the master switch or Athletics is
off. Every one of these is read live, every frame (a switch flipped mid-battle takes the bar
away or brings it back at once).

**Outside a battle — the player bar only (step 12, Anton's playtest: the training field showed no
bar, because it runs in the game's walk-about mode, not a battle mode).** With
`ShowPlayerBarOutsideBattles` on, the player bar also shows in the walk-about mode (the game's
`StartUp`: the training field, towns, villages, a lord's hall, arena practice) — with the player on
the field and his Athletics tracked — while he **holds a weapon or a shield** (anything wielded in
either hand; fists only do not count) **or his Athletics is below full** (below the top it can
refill to — a wound's cap counts as full), so it stays up while it refills; one second after both
end it goes (a short grace, so a weapon switch — both hands empty for a moment — does not flicker
it). Never in a conversation, barter, deployment, cutscene or replay. The orders-menu strip keeps
the fights-only rule. The log names the reason at each appearance and removal outside a battle
("outside a battle: a weapon drawn", "… no weapon drawn and your Athletics full"), and the
summary's `hud:` line ends with "outside a battle: shown Nx (weapon drawn N, refilling N), on
screen N s".

**The player bar as built (step 6):** bottom right, one row under the vanilla health bar (and
the horse bar), its right end lined up with the health bar's fill: the word *Athletics*, the
number `current / pool` (current rounded UP, so it reads 0 only when truly empty), and a slim
bar (`PlayerBarWidth` × `PlayerBarHeight`, placed by `PlayerBarOffsetRight` /
`PlayerBarOffsetBottom` — UI pixels of the 1080p layout, the game's UI scale applies). Inside
the bar: the fill in the colour of f, a white marker at the peak line, the part the wounds hold
dark red-brown (from the usable share to the end — `HealthCapsAthletics`), the rest dark grey.
Empty (E = 0): the word reads *Exhausted* and the word, the number and the bar's frame turn
red. Refreshed every `HudRefreshSeconds`. Since step 13 its row sits at 30 px (it was 54) so the
Attack recovery bar fits above it, both under the vanilla health and horse bars (the config format
2 moves a config.json that still holds the old 54).

**The Attack recovery bar as built (step 13):** one slim row just above your Athletics bar (its
row `RecoveryBarOffsetAbove` (24) px higher — it moves with your bar — its right end lined up with
it, `RecoveryBarWidth` × `RecoveryBarHeight`, 205 × 14 px): the words *Attack recovery* and a bar.
- It IS your no-attack pause (`AttackRatePlayerTimer`): **EMPTY** from your attack's release when a
  pause will follow (below the peak line), **filling** over the pause once the attack ends, with
  **the seconds left inside it** ("1.3 s", tenths rounded up, so it never reads 0.0 while it
  still runs), **FULL** (steel, no text) whenever no pause runs — at full strength it simply stays
  full. The fill is amber while it refills. You cannot start an attack until it is full; keep the
  button held and the attack starts the moment it fills.
- **With the swing stack** (step 32, §2): its words read **"Swings 4/8"** (your stored swings / how many
  you can hold now - rebuilt only when either changes) instead of "Attack recovery"; the fill is the refill
  toward your NEXT stored swing (steel and full at the cap, amber below it, frozen while you attack); the
  seconds inside only while you wait at 0. You can attack whenever the count is above 0.
- **It flashes** (`FlashBarOnEarlyAttack`, on): a white overlay, two quick pulses (0.12 s on,
  0.08 s off — a UI constant like the bars' colours), when you press attack while it is not full
  (the press does nothing); hammering does not restart a flash that still runs.
- Shown by the Athletics bar's own rules (fights, and outside a battle with
  `ShowPlayerBarOutsideBattles`), with its own switch, and only while the Athletics bar
  (`ShowPlayerBar`) and your pause (`AttackRatePlayerTimer`) are on — with your pause off there is
  nothing to recover. The fill, the seconds and the flash update every frame (a 0.1 s refresh
  would stutter); the layout at `HudRefreshSeconds`. Native sprites and brushes only.
- Logged like the Athletics bar: `[hud] attached: recovery bar …`, `layer created … movie
  TraxAttackRecoveryBar loaded OK`, `first values pushed …`, the first pause and the first flash of
  the battle in full, and the summary's `hud: recovery bar …` lines (pauses shown, flashes).

**Additions (Anton, 2026-09-27):**
- The player bar (and, LATER, the target bar) shows the Athletics NUMBER (current / pool) and a
  marker at the peak line (`AthleticsPeakPercent`, 75% of the pool). Fill colour by f (the share
  of the peak line left): in the peak zone green; below the line blue; f at or below
  `BarYellowBelowPercent` (75) % yellow; `BarOrangeBelowPercent` (50) orange;
  `BarRedBelowPercent` (25) red. The orders-menu strip uses the same colours.
- Squads also show their AVERAGE HEALTH (`ShowFormationHealth`, on) — in the orders menu (built,
  step 9) and under the formation markers while ALT is held (built, step 20).
- Orders menu: our strip sits directly UNDER the vanilla formation cards, one cell per card
  ("below the arrows remaining", Anton's words) — Athletics average ± spread and average
  health. Still no UIExtenderEx; if the cards' positions cannot be matched reliably, the
  step falls back to a compact panel and says so. (Built in step 9 — item 4 above.)

## 4. Configuration

Every parameter lives in two places that stay in sync:

- a config file created on first run under
  `Documents\Mount and Blade II Bannerlord\Configs\TraxCombatEnhancements\` — hand-editable,
  with a plain-words explanation beside every value;
- the **Mod Configuration Menu (MCM)** when installed. MCM must stay OPTIONAL: without it the
  mod runs on the file alone (see CLAUDE.md, hard requirements).

**What the player reads** (step 10b; step 20 added the tenth, step 21 the eleventh): eleven groups, the same in MCM and the
file, in this order — *Master switch, Damage randomness, Athletics, Tired fighters, Tired fighters step
back, Battle pace (AI), Refill, Your Athletics bar, Orders menu strip, Hold ALT: formation markers, Athletics and fever bars (step 46's name; step 44's was "... and fever marks", step 20 called it Formation markers (hold ALT)), Advanced* (later steps added Damage reduction's rename, Brace, Sprint, Aggression and fever - 14 groups since step 43); MCM shows
its *Defaults* buttons (§2c) just above
*Advanced*, which stays last. **Since step 59 (decision 51) MCM shows TWO pages**: *Trax Combat Enhancements* - the
short player page: *Master switch* and six plain groups, 20 settings (the list: Parameters), then *Defaults* with
"Revert all to defaults" - and *Trax Combat Enhancements - Advanced* - every other setting in the groups above (Damage
first, the Defaults buttons, Advanced last). Each setting on exactly one page; the file's sections in the same order, the
player page's first. One vocabulary: the pool, the bar and its points are *Athletics*; the
character-screen skill is *the Athletics skill*; the *peak line* is the white mark on the bar
(`AthleticsPeakPercent`) — at or above it a fighter is at full strength; *empty* = 0 Athletics.
Every label carries its unit — (points) (%) (x) (m) (s) (px) (MB). Every MCM hint ends
"Applies at once, even mid-battle." and the default; the config file's header says a hand edit
applies at the next battle start.

**Master switch** (`ModEnabled`, Anton 2026-09-27, built in step 5b): one checkbox at the top
of MCM (and the first key of the config file) turns the WHOLE mod off — live, even mid-battle —
so the same battle can be fought with and without it and compared. Off: the damage roll hands
back the game's own number; nobody pays Athletics, nobody refills; every speed penalty (attack,
run, horse) is lifted at once (the stat decorator checks the switch itself, so even a recompute
before the logic's next tick is vanilla - the AI's attack values included, 5e); the player bar,
the Attack recovery bar, the orders-menu strip and the numbers under the formation markers (steps 6,
13, 9, 20) hide; tired fighters never step
back (5d) and every no-attack timer - yours and every AI fighter's - is released at once (5e, 13).
Back on: everyone starts with a full Athletics bar — a fresh start, not a resume. The two model
decorators stay registered (they cannot be removed mid-game) and pass everything through; the
tournament AI-level fix, which only keeps vanilla behaviour intact, stays. **Logging stays on**:
the mission start line and the `[summary]` header say `mod ON` / `mod OFF`; a toggle
mid-battle is logged with its time, and the header then says "mod was on for N% of the battle
(started ON; OFF at 40.1 s, …)". The summary's vanilla-observable lines (duration, killed /
knocked out / fled, agents) are unchanged, and while off the damage lines record the game's own
hit numbers unrolled (factor 1, "damage while the mod was OFF … avg N per hit") so an ON run and
an OFF run compare line by line. Every feature checks `ModEnabled` FIRST (CLAUDE.md).

**Hot swap**: a change in MCM applies LIVE — the next hit, blow or HUD refresh uses it, even
mid-battle. Nothing ever needs a game restart; if some parameter can only apply from the
next battle, its description says so. Hand edits to the file are picked up at the next
battle start.

**When MCM writes the file** (its Done button), a hand edit made meanwhile is never lost: the
file on disk is re-read at that moment, MCM's value is written only for the settings MCM
changed, and every other setting keeps the value on disk (it takes effect at the next battle
start). Unknown keys are kept in a "not recognised" section; a file that does not parse is
saved as `config.json.broken-<time>` before a fresh one replaces it. MCM's Reset buttons
(and "Revert all to defaults") restore the defaults from `defaults.json` (§2c). Ranges live in the schema
(`src/TraxCombat.Core/SettingsSchema.cs`) and are printed beside each key in the file. The
file also carries `ConfigVersion`, a format stamp — not a setting.

**The log** (`trax_combat.log`, beside config.json; one rolling file across game starts): every
line is `yyyy.MM.dd HH:mm:ss.fff [tag] message`; a VERBOSE line (written only while
`VerboseLogging` is on, rate-limited per kind) carries a `~` before its tag:
`… ~[damage] melee on a person: …`. Past `LogMaxMegabytes` (8) the file is trimmed to about half
of it, and a trim never loses the lines the playtest is read from (step 10b, review R7):
- KEPT: every line that is not verbose — load, settings, MCM, mission start and end, every
  feature's "mission start" / "first … this battle" / "YOU …" line, the `[summary]` blocks,
  `[error]` with its stack, the `[log]` notes — and any `[load]` `[compat]` `[config]` `[mcm]`
  `[mission]` `[summary]` `[error]` line even if written verbose;
- CUT: the oldest verbose lines, at one point in time (a newer one is never cut while an older one
  stays). The trim's note, `[log] (log trimmed at … : N older verbose lines cut, up to …)`, sits at
  the top of the file.
- Only if the kept lines ALONE pass half the limit (dozens of battles over several game starts)
  do the oldest of them go too — the file stays bounded. Turning `VerboseLogging` on can never
  cost a line the log would have had with it off.

## 5. Compatibility

- **RBM (Realistic Battle Mod) is NOT compatible** (Anton, 2026-09-27): it has its own
  posture and stamina and patches the same combat. The Steam page says so plainly. If RBM
  is enabled, the mod logs `[compat]` and shows ONE message at the main menu / campaign
  start ("Trax Combat Enhancements is not compatible with Realistic Battle Mod — disable
  one of them"). It does not refuse to run.
- Other combat mods that change damage or attack speed may stack with ours; the log's
  per-battle summary is the tool to see it.
- **One copy runs** (step 11): with two copies of this mod enabled at once (the dev install and
  the Workshop release), the first to load runs and the other stands down in every hook (no log
  line, config, MCM page, model or mission logic of its own) - otherwise both damage decorators
  would roll every hit. The running copy logs `[compat]` and shows ONE message at the main menu.
- **Save-safe**: nothing is added to the campaign save (no saveable types, no campaign
  behaviours) - the mod can be added or removed mid-campaign.

## Parameters

The Default column is the INITIAL value; the shipped default is whatever `defaults.json`
says (§2c).

**MCM pages (step 59, decision 51).** The PLAYER page ("Trax Combat Enhancements" - the file's first sections) holds exactly these
20, in this order: `ModEnabled`; *How hard hits land* `DamageRandomPercent`, `DamageReductionPercent`; *Tiredness (Athletics)*
`AthleticsEnabled`, `CostPerBlow`, `HeroCostMultiplier`; *How the AI fights* `AiAggressionFreshPercent`, `AiAggressionExhaustedPercent`,
`FeverEnabled`, `ShieldWallHoldsSlots`, `ShieldWallStepOutMetres`, `CourageEnabled`, `CavalryFlockEnabled`; *Staggers and knockdowns*
`ImpactEnabled`, `ImpactOnPlayerPercent`; *Sprinting* `SprintEnabled`, `SprintKey`; *On screen* `ShowPlayerBar`, `AltMarksEveryone`,
`ImpactShowMessages`. Every other key is on the ADVANCED page ("Trax Combat Enhancements - Advanced"), in its system's group. A new
row goes to the Advanced page unless a player needs it to set up his game (CLAUDE.md).

| Key | Default (initial) | What it does |
|---|---|---|
| `ModEnabled` | true | THE master switch. Off = the whole mod steps aside and every battle is pure vanilla (live, even mid-battle); the log still records each battle for comparison. |
| `DamageRandomEnabled` | true | Master switch for damage randomness. |
| `DamageRandomPercent` | 50 | ± spread in percent. 50 → a 50-damage hit lands for 25–75. 0 = off. |
| `DamageRandomMelee` | true | Randomize melee hits. |
| `DamageRandomRanged` | true | Randomize arrows, bolts and thrown weapons. |
| `DamageRandomOnMounts` | true | Randomize hits that land on horses too. |
| `DamageRandomOnShields` | false | Randomize the damage a shield takes when it blocks. |
| `SprintFullCritical` | true | Step 62 (decision 52), THE UNCAPPED CRITICAL: a CHARGING attacker's MELEE blow on foot (the impact model's charge - sprinting, or his first blow within `ImpactChargeCarrySeconds` after a sprint; read, never spent, by the damage decorator) rolls the ±p roll's FULL upside whatever his f (`DamageBonusFollowsAthletics` does not shrink it). Missiles and riders never. Live. |
| `DamageReductionPercent` | 25 | Step 25 (Anton): every hit on a person, a horse or a shield deals this % less, AFTER the roll (an average 100 hit lands 75) - melee, ranged, kicks, bashes, charges, whether the roll is on or off; falls and objects never. A positive hit never below 1. 0 = the game's damage; range 0-90. |
| `AthleticsEnabled` | true | Switch for Athletics (the stamina bar). |
| `AthleticsPoolFloor` | 50 | Smallest possible Athletics pool (for anyone with a low Athletics skill; 0 = none, never below 1 point). |
| `AthleticsPoolPerSkill` | 1.0 | Pool per point of Athletics skill (1.0 → the bar tops at the skill). |
| `AthleticsPeakPercent` | 75 | At or above this % of their own pool a fighter is at full strength (green); below it they weaken in a straight line to 0. |
| `HealthCapsAthletics` | true | Health left caps the usable pool. |
| `CostPerBlow` | 10 | Athletics points one blow costs before multipliers (points, whatever the pool). defaults.json 3 since step 43 (Anton: the AI's eagerness now follows its bar; config format 8 moves a file's old 10 once), 7 since step 46 (Anton's playtest; config format 9 moves a format-8 file's 3 once), 8 since step 68 (Toni's 100 v 100 of 2026-10-19: "tire the men more to slow down the combat again a bit after the first impact" - a recruit's 50-point bar empty after 7 blows, a legionary's 130 after 17; hero 6 / party leader 4.5; config format 14 moves a file's 7 once, a tuned value kept and said; decision 61), 10 again since step 71 (Toni 2026-10-19: "back to 10 for the swings ... they need to tire faster still" - a recruit's 50-point bar empty after 5 blows, a legionary's 130 after 13; hero 7.5 / party leader 5.63; config format 15 moves a file's 8 once, a tuned value kept and said; decision 65). |
| `CostPerKickOrBash` | 3 | Step 18 (Anton): Athletics points one kick or shield bash costs before the hero and party-leader multipliers (3 / 2.25 / 1.7), once, when it starts, landed or not; never starts the attack pause. 0 = free (steps 5-17). Step 66 (Anton 2026-10-19: "make the kick/bash also a bit more taking of the energy, say 5 pts each"): 5 in defaults.json (5 / 3.75 / 2.8 - you and the AI alike; config format 13 moves a file's 3 once). Step 71 (Toni, decision 65): 6 (6 / 4.5 / 3.38; config format 15 moves a file's 5 once). Step 81 (Toni 2026-10-22: "make all those moves take 8 athletics each"): 8 in defaults.json (8 / 6 / 4.5; config format 19 moves a format 15-18 file's 6 once); swings stay 10. |
| `CostPerShieldBlock` | 1 | Step 22 (Anton): Athletics points the DEFENDER pays for a melee blow he blocks with his shield on the correct side, before the hero and party-leader multipliers (1 / 0.75 / 0.56); once per blocked blow; does not restart the refill delay while `OnlyWrongSideBlocksStopRefill` is on (step 29); never an attack pause or a step back. 0 = free (steps 5-21). Step 68 (Toni, decision 61): 2 in defaults.json (2 / 1.5 / 1.13; config format 14 moves a file's 1 once). Step 71 (Toni, decision 65): 3 (3 / 2.25 / 1.69; config format 15 moves a file's 2 once). |
| `CostPerWrongSideShieldBlock` | 5 | Step 22 (Anton): the same for a blow his shield stops on the WRONG side (the engine's `CorrectSideShieldBlock` false) - 5 / 3.75 / 2.81. 0 = free. defaults.json 2 since step 43 (Anton; config format 8 moves a file's old 5 once), 5 again since step 68 (Toni, decision 61: a clumsy block tires - 5 / 3.75 / 2.81; config format 14 moves a file's 2 once), 6 since step 71 (Toni, decision 65: 6 / 4.5 / 3.38; config format 15 moves a file's 5 once). |
| `CostPerWeaponParry` | 2 | Step 22 (Anton): the same for a blow he blocks or parries with a weapon, no shield (a chamber block too) - 2 / 1.5 / 1.13. 0 = free. Step 71 (Toni, decision 65): 3 in defaults.json (3 / 2.25 / 1.69; config format 15 moves a file's 2 once). |
| `OnlyWrongSideBlocksStopRefill` | true | Step 29 (Anton): true = only a WRONG-side shield block restarts the refill delay (and ends a refill run); a right-side shield block and a weapon parry still cost but the refill keeps running underneath. false = every paid block restarts it (step 22). Attacks, kicks and bashes restart it either way. Live. |
| `ShieldFrontArcIsCorrect` | true | Step 64 (decision 55, Anton's looter battles 2026-10-19: ~85% of an AI shieldman's blocks read WRONG side even fresh - the native AI guesses its guard direction, a wall's poses are fixed): a melee SHIELD block whose attacker stands within `ShieldFrontArcDegrees` of the blocker's facing (step 62's angle) counts as a RIGHT-SIDE block for every system of ours - the block cost and the refill rule (steps 22 / 29), the guard lines (30 / 34), the impact model's event (the correct block's ceilings, the riposte, the thrown back) - whatever the engine's `CorrectSideShieldBlock`; outside it the engine's verdict stands. The arc corrects the GAME's own guess, not step 30's: a tired AI man's share w = `TiredBlockRandomPercent` × (1 − f) (his `BlockRandomWeight`) of front blocks keeps the engine's verdict (a stateless roll per blow - `ShieldArcMath.TiredRoll` of both men and the swing - so every system agrees), so an exhausted wall still breaks (an empty legionary's front block: 40% down / 90% staggered as before; a fresh one's right-side). Weapon parries keep the engine's direction. You and the AI alike. The engine's own shield damage untouched. The [summary] "shield front arc" line counts the engine's verdict against ours. Live. |
| `ShieldFrontArcDegrees` | 60 | Step 64: the front arc's half-angle - the attacker within this many degrees left or right of the blocker's facing. Range 0-180. Live. |
| `CostOnMiss` | true | true: every attack costs, landed or not. false: only blows that land. |
| `HeroCostMultiplier` | 0.75 | Cost multiplier for heroes. |
| `PartyLeaderCostMultiplier` | 0.75 | Extra multiplier for a party's leading hero, on top of the hero one. |
| `ExhaustedAttackSpeedPercent` | 20 | Attack speed (the attack RATE) at 0 Athletics, percent of normal (a straight line up to 100% at the peak line) - since step 13 delivered by the no-attack timer D × (1/m − 1) after each attack. |
| `AttackRatePlayerTimer` | true | Step 13: your own no-attack timer - after each of your attacks below the peak line the attack button does nothing until it ends (held, it attacks the moment it ends); blocking, kicks, moving, weapon switches always work. Off: your attacks are never held. |
| `AttackRatePaceHold` | true | Step 13: the AI's no-attack timer - after each attack (melee and ranged, on foot and mounted) a tired AI fighter may not start another for D × (1/m − 1), guard up (A/B switch). |
| `AttackRatePaceByInput` | true | Step 16: the AI timer through the AI's own input - only the attack bits are taken out, his blocks, parries and moves stay his own. Off = step 13's NoAttack flag (held men blocked 2-13%). A/B switch; a running pause finishes the way it began. |
| `AiHoldRaiseGuard` | true | Step 16: a held AI fighter (his pause or a step back, the new techniques) who wants to attack raises his guard instead (`DefendDown` - a block, the shield). Off: the attack is only dropped. |
| `AttackRateAiDecisions` | false | Tired AI fighters also decide to attack (and riposte) less often, loose less readily and aim longer - × / ÷ their attack speed (A/B switch). Off since step 13: on top of the timer it double-counts (the log read 128% / 172% too slow). |
| `AttackAnimationMinPercent` | 100 | The attack animations (swing, thrust / draw / throw, reload) at empty, in % - step 20b: a straight line A + (1 − A) × f up to full speed at the peak line (steps 13-20: max(m, this %)); 100 = always full speed (the whole slow-down is the timer). D is taken at full animation speed, so the pause in seconds never grows with it. Range 5-100; defaults.json 85 since step 20b. |
| `SwingStackEnabled` | true | Step 32 (Anton, 2026-10-12): the SWING STACK - every fighter stores swings (cap = max(`SwingStackMinimum`, round(current points / `SwingStackPointsPerSwing`)), half away from zero; spawn full); an attack (melee, a shot, a throw) spends one and is held only with none left - then exactly until the next comes back; one comes back per the pause that attack would have left (D × (1/m − 1) at his CURRENT m, + the AI's battle-pace share) of time NOT attacking (an AI melee man's own gap `AiMeleeGapSeconds` after a swing from the stack does not count). Off = step 13's pause after every attack. Live. |
| `SwingStackPointsPerSwing` | 20 | Step 32: Athletics points per stored swing (pool 150 full = 8, 75 points = 4; pool 50 full = 3). Range 5-100. |
| `SwingStackMinimum` | 2 | Step 32: the stack never holds fewer than this - even at empty. Range 1-10. Step 81 (Toni 2026-10-22: "make the min swing stack from 2 to 3, to stack up a bit more, but make sure kicks and bashes use it"): 3 in defaults.json (config format 19 moves a file's 2 once); an AI kick / bash still spends one and cannot start at 0 (`KicksUseSwings`). |
| `KicksUseSwings` | true | Step 32: kicks and shield bashes spend a stored swing too (never a pause of their own); at 0 they cannot start - your kick key is taken out by the gate, an AI man's kick out of his input while he waits. Off: free of the stack. |
| `MinMoveSpeedMultiplier` | 0.7 | Top speed on foot at 0 Athletics (0.3 until step 14 - Anton: "too slow, unrealistic"; defaults.json 0.6 since step 20b - Anton's "sweet spot"; defaults.json 0.3 again since step 22 - Anton, for slower battles). |
| `MountMinSpeedMultiplier` | 1.0 | Horse top speed at the rider's 0 Athletics (1.0 = horses never slow). |
| `DamageBonusFollowsAthletics` | true | The damage upside shrinks with the attacker's Athletics below the peak. |
| `TiredBlockRandomPercent` | 100 | Step 30 (Anton, 2026-10-12): a tired AI man's block DIRECTION is partly left to chance - weight w = this % × (1 − f); the game's own `AiRandomizedDefendDirectionChance` (clamped to 0-1) becomes own + (1 − own) × w (f 1 untouched, f 0.5 at 100 = half his picks random, empty = all). AI only, riders too, never the player. 0 = off (the game's blocking). Live. |
| `StepBackEnabled` | true | Tired AI fighters on foot step back after melee swings (§2). Off mid-battle: everyone stepping back returns to his formation at once. |
| `StepBackBackpedal` | true | Step 16: the step back is a backpedal through the AI's own input (a backwards movement, facing his enemy) until `StepBackDistance` is covered or `StepBackSeconds` run out. Off = step 5d's scripted walk (80% turned their backs). A/B switch; a running step finishes the way it began. |
| `StepBackMaxChancePercent` | 100 | Chance to step back at 0 Athletics (0 at the peak, straight line between). |
| `StepBackDistance` | 2.0 | Metres a fighter steps back, straight away from his enemy. |
| `StepBackSeconds` | 1.5 | Longest a step back lasts before the formation takes over again. |
| `StepBackEnemyRange` | 4.0 | Steps back only while the enemy he fights is within this many metres. |
| `StepBackHoldAttacks` | true | No swings while stepping back (guard up only). |
| `StepBackMaxAtOnce` | 50 | Most fighters stepping back at the same time, all sides together. |
| `ShieldInfantrySwingsLessPercent` | 30 | Step 21 (Anton): AI melee on foot with a shield in the other hand (read at each swing) swings this % less, fresh or tired - after each swing a pause that stretches his expected cycle (his attack + his tired pause + `AiMeleeGapSeconds`) ÷ (1 − %), guard up (§2 "Battle pace"). Never you, never riders. 0 = off; range 0-90; applies at the next swing. |
| `FootMeleeSwingsLessPercent` | 15 | Step 21: the same for AI melee on foot WITHOUT a shield (two-handers, polearms, a one-hander alone). 0 = off. |
| `AiMeleeGapSeconds` | 1.0 | Step 21: the AI's own gap between a melee attack's end and its next one at full strength (measured 1.0 s in Anton's logs of 2026-09-27/28) - the two "swing less" pauses are sized on it. |
| `ExtraPauseAfterBowShotSeconds` | 2.0 | Step 21 (Anton): AI bow shots (on foot and horse archers) wait this many seconds more, on top of the tired pause - about 30% fewer shots (fresh AI bow cycle ~4.5 s). 0 = off; applies at the next shot. |
| `ExtraPauseAfterCrossbowShotSeconds` | 2.5 | Step 21: the same for AI crossbow shots (fresh cycle ~6 s). Thrown weapons and slings: none. 0 = off. |
| `ShieldWallPullBackSeconds` | 0.5 | Step 63 (decision 54), THE WALL PULLS MEN BACK: a HELD man (step 41's flag, not stepping out on step 42's leash) more than max(L, 1) + 0.25 m ahead of his slot is OUT OF PLACE - looked at every 0.1 s and held again at each look; once he has stood out this long able to move (not down, not reeling from an impact) he is walked back to within max(L, 1) ÷ 2 of his slot: a backpedal facing the enemy through step 16's input hook (a wish of its own, `WallPull`), at most 4 s a spell. 0 = the game's flag alone (and no extra looks: step 42 exactly). Advanced, "Battle pace (AI)" (the wall's own two settings are on the player page). Range 0-3. Live. |
| `ShieldWallAdvanceAsOne` | true | Step 68 (decision 61, Toni's 100 v 100 of 2026-10-19: a shield wall ordered forward with F1 → F4 = ADVANCE had 15.4% of its men in contact more than 1 m ahead of their slots, 5.8% more than 2 m - the looter battle with the wall standing 0.8%; "they need to hold the wall and push the whole wall, not to break up"), THE WALL PUSHES AS ONE: under an advance the game's ORDER slot of each man lies at the enemy's line (`MovementOrder.GetPositionAux`: the closest enemy formation's median less 2 m) and every AI man walks to his order slot (`Agent.TrySetFormationFrame`, every ~0.5 s) - the men free to move walk into the enemy while their mates fight, the line stretches. On: a man step 41 HOLDS under a moving order (advance - yours or the AI's -, move, fall back, follow; never a stop / hold, never a charge) whose formation's front is within `ShieldWallAdvanceNearMetres` of the nearest enemy formation's front is sent to his slot in the line as it stands + at most `ShieldWallAdvanceStepMetres` toward the game's order slot (`WallPaceMath.Pace`; written with vanilla's own call right after vanilla's update and at step 41's looks; a man stepping out on the leash keeps it) - the whole line advances at the pace its fighting front allows, the leash still lets a man strike. Off (or the hold off, or the master switch) = the game's own frame back at once. The [summary] lines "wall slots ON by order" (the in-contact leak: standing / ADVANCE / moving) and "wall advance as one". Advanced, "Battle pace (AI)". Live. |
| `ShieldWallAdvanceStepMetres` | 1.0 | Step 68: how far ahead of his slot in the line a man of an advancing wall is sent each moment (m) - the push's pace and the most a line stretches. Smaller = tighter and slower (below ~0.3 m the engine's stop tolerance may never walk him). Range 0.3-3. Live. |
| `ShieldWallAdvanceNearMetres` | 10.0 | Step 68: the advance as one begins when the gap between the wall's front and the nearest enemy formation's front (their centres' distance less half of each one's depth) is at most this (m); farther = the game's own march (unchanged). 0 = only once the fronts meet. Range 0-60. Live. |
| `ShieldWallKickReturnMetres` | 0.5 | Step 68 (decision 61): THE WALK BACK AFTER A KICK - a held man whose OWN kick / bash began within 2.5 s (`WallPullMath.KickWindowSeconds`) or who presses a punish window (step 54's step-in) is out of place from this many metres ahead of his slot (not step 63's 1.25) and walked back at once - step 63's backpedal, no `ShieldWallPullBackSeconds` wait - as soon as he can move (not down, reeling or in his kick), until within half of it. 0 = off (step 63's rule only). The "wall pulled back" line counts these spells apart. Range 0-3. Live. |
| `ShieldWallHoldsSlots` | true | Step 41 (Anton, 2026-10-17): every AI man on foot in a shield wall, square or circle keeps his slot instead of walking out of it at the enemy he fights (the engine's "keep with the formation" flag, rewritten right after vanilla's own update - §2 "The shield wall holds its slots"). Any order but a charge or a retreat; not detached, fleeing, or far from his slot AND behind the line; both sides; never you, riders, a siege's attackers, naval battles. Live: off = vanilla's own value written back at once. |
| `ShieldWallStepOutMetres` | 1.0 | Step 42 (Anton, 2026-10-17, his playtest of 41: held men reached and stopped, rarely hitting an enemy who stepped back): the LEASH - a held man may step up to this many metres out of his slot at an enemy close in front of it (within this + 2 m of the slot), then is held again and walks back; he may step again once back within 0.3 m (at most half the leash) of his slot (§2 "The shield wall holds its slots"). 0 = step 41's hard hold exactly. 0-3. Live. |
| `ShieldWallStandFirm` | true | Step 75 (Toni 2026-10-22: "the men in the shield wall still step forward even when on a standing order, they take a few steps to strike at an enemy, then fall back" - a switch to test on and off; decision 69), A STANDING WALL NEVER LEAVES ITS SLOT: a man step 41 HOLDS whose formation (a shield wall, square or circle) is on a STOP, or on a MOVE that has reached its place (`ShieldWallStandFirmArrivedMetres` / `ShieldWallStandFirmStillMetresPerSecond`, latched - `StandFirmMath.Conditions`), STANDS FIRM: step 42's leash is 0 for him (a step running is reined at once), a running AI sprint ends (`SprintEnd.StandFirm`), step 68's advance pace never applies, the engine's walk target is his slot (`ShieldWallStandFirmPinFrame`) and the tolerance it is told the slack (`ShieldWallStandFirmTightTolerance`); more than `ShieldWallStandFirmSlackMetres` ahead of his slot - his own kick / bash, a punish press, the press, the game's approach, a blow - he is walked straight back (step 63's backpedal, no wait) to within half of it, never while his kick plays. He keeps his guard, blocks, swings and kicks / bashes at what is within his reach (nothing touches his attack input). Never under an advance (yours or the AI's), a charge, fall back, follow or a retreat; step 67's morale break (`FearWallBreakMorale`) and step 41's escape (far behind) still let go; courage, the fear flock and the block hold never touch a held man (steps 49 / 67 / 74). Gate: `ModEnabled` FIRST, then `ShieldWallHoldsSlots`, then this. Both sides' AI formations, never the player himself. The `[wall]` stand-firm lines and the [summary] "wall stands firm" line (ON vs OFF in contact, strikes from the slot, the levers refused by kind). PLAYER page, "How the AI fights". Live. |
| `ShieldWallStandFirmSlackMetres` | 0.3 | Step 75 (decision 69): a man STANDING FIRM more than this ahead of his slot (along the facing; outward for a square / circle) is out of place whatever moved him and walked straight back - step 63's backpedal facing the enemy, at once once he can move (not down, reeling or in his own kick) - to within half of it; also the most tolerance (the integrity data's deviation) the engine is told for him (`ShieldWallStandFirmTightTolerance`). Advanced, "Battle pace (AI)". Range 0.1-1.5. Live. |
| `ShieldWallStandFirmArrivedMetres` | 2.0 | Step 75 (decision 69): "AT ITS PLACE" for a MOVE - its formation's position (`Formation.CurrentPosition`) within this of its order position (`Formation.OrderPosition`) while its men's average speed is at most `ShieldWallStandFirmStillMetresPerSecond`; then latched until the order position moves more than this (a new move) or the formation stands more than 2 x this from it (`StandFirmMath.LeaveFactor` - pushed far, the game walks it home as a line). A STOP stands at once. Advanced, "Battle pace (AI)". Range 0.5-20. Live. |
| `ShieldWallStandFirmStillMetresPerSecond` | 0.5 | Step 75 (decision 69): a moved wall is "at its place" only once its men's average velocity (`CachedFormationIntegrityData.AverageVelocityExcludeFarAgents`) is at most this - a wall still walking in is not frozen early. Advanced, "Battle pace (AI)". Range 0-3. Live. |
| `ShieldWallStandFirmPinFrame` | true | Step 75 (decision 69): for a man standing firm the formation FRAME - the point the engine walks him to (`Agent.TrySetFormationFrame`, step 68's call) - is his slot, written right after vanilla's ~0.5 s update and at step 41's looks: under a STOP vanilla's frame is his OWN position (`Formation.GetOrderPositionOfUnit` → `unit.GetWorldPosition()` for `StandGround`), so a man who drifted out was never walked back by the game. Off = the game's own frame. Every let-go path gives the game's frame back. Advanced, "Battle pace (AI)". Live. |
| `ShieldWallStandFirmTightTolerance` | true | Step 75 (decision 69): for a man standing firm the "keep your slot" write (`Agent.SetFormationIntegrityData`) hands the engine a deviation of at most `ShieldWallStandFirmSlackMetres` instead of the formation's own (`DeviationOfPositionsExcludeFarAgents` - a loose wall's spread lets a man wander that far). The native meaning is unseen (UNVERIFIED - PLAYTEST D40's A / B). Off = the formation's own. Advanced, "Battle pace (AI)". Live. |
| `SpearPreferenceEnabled` | true | Step 76 (Toni 2026-10-22: "my shield wall-ers have spears but they keep using their swords, even though the spear has a longer reach"; decision 70), THE AI PREFERS THE SPEAR - RBM's weapon preference without Harmony: an AI man (both sides, never you; on foot or mounted) whose four weapon slots hold a melee POLEARM and a melee SIDEARM (`SpearMath.Classify` - a thrown / consumable / ranged item is neither; the loadout re-read only when a slot's item changes) is looked at every ~`SpearLookSeconds` (jittered, after deployment); the stat decorator writes after the base model (which assigns 1 / 1 / 1 every recompute) `AiWeaponFavorMultiplierPolearm` x `SpearPolearmFavor` while no enemy is close, `AiWeaponFavorMultiplierMelee` x `SpearSidearmFavor` once one has been close (`SpearMath.Step` - RBM's two timers); a flip recomputes him (`Agent.UpdateAgentProperties`, the tick's budget). CLOSE = an enemy within his close radius (`SpearMath.CloseRadius` - from his longest polearm's length) not running away, not holding a polearm himself (`SpearKeepVsPolearm`); a rider faster than `SpearRidingFastMetresPerSecond` has nothing close; a back-rank man of a wall / a halted line keeps the polearm whatever is close (`SpearBackRankKeeps`). Never a query of nearby agents inside the stat model (RBM's Agent.Build deadlock). Gate: `ModEnabled` FIRST, then this. Off: the game's 1 / 1 / 1 at the next recompute (every favoured man recomputed); the looks still measure (the [summary] "spears" lines - time holding the polearm / the sidearm by condition, ON vs OFF). PLAYER page, "How the AI fights". Live. |
| `SpearPolearmFavor` | 35 | Step 76 (decision 70): the AI's polearm favor (`AiWeaponFavorMultiplierPolearm`, vanilla 1) while no enemy is close - RBM's 35. Range 1-100. Advanced, "Battle pace (AI)". Live (every favoured man recomputed). |
| `SpearSidearmFavor` | 55 | Step 76 (decision 70): the AI's melee favor (`AiWeaponFavorMultiplierMelee`, vanilla 1) once an enemy has been close - RBM's 55 (the polearm's favor then 1). Range 1-100. Advanced, "Battle pace (AI)". Live. |
| `SpearSidearmAfterSeconds` | 2.0 | Step 76 (decision 70): RBM's SidearmDelay - an enemy must stay close this long (one spell, looks in a row) before the sidearm is favoured. Range 0-20. Advanced. Live. |
| `SpearPolearmBackAfterSeconds` | 4.0 | Step 76 (decision 70): RBM's MainWeaponDelay - the polearm favoured again this long after the last close enemy. Range 0-20. Advanced. Live. |
| `SpearPolearmBackMountedSeconds` | 2.0 | Step 76 (decision 70): RBM's MountedMainWeaponDelay - the same for a rider (he needs his lance for the next charge). Range 0-20. Advanced. Live. |
| `SpearRidingFastMetresPerSecond` | 4.0 | Step 76 (decision 70): RBM's MountedPassingSpeed - a rider whose horse moves faster than this is riding through: no scan, nothing close. Range 0-15. Advanced. Live. |
| `SpearCloseFromLength` | true | Step 76 (decision 70, Toni's addition a): the close radius from the polearm's length (`SpearCloseBaseMetres` + `SpearCloseLengthPercent` of his longest melee polearm's `WeaponLength`, clamped to `SpearCloseMinMetres`-`SpearCloseMaxMetres`); off (or a length not known) = `SpearCloseFallbackMetres` for every spear. Advanced. Live. |
| `SpearCloseBaseMetres` | 1.0 | Step 76 (decision 70): the close radius's base (m). With 60%: a 1.5 m spear 1.9 m, 2.0 m 2.2 m, 2.5 m 2.5 m, 3.0 m 2.8 m. Range 0-3. Advanced. Live. |
| `SpearCloseLengthPercent` | 60 | Step 76 (decision 70): the share of the polearm's length added to the base (%). Range 0-150. Advanced. Live. |
| `SpearCloseMinMetres` | 1.5 | Step 76 (decision 70): the close radius at least (m). Range 0.5-6. Advanced. Live. |
| `SpearCloseMaxMetres` | 3.5 | Step 76 (decision 70): the close radius at most (m; never below the minimum). Range 0.5-6. Advanced. Live. |
| `SpearCloseFallbackMetres` | 2.5 | Step 76 (decision 70): the close radius when the length is not known or `SpearCloseFromLength` is off - RBM's fixed 2.5 m. Range 0.5-6. Advanced. Live. |
| `SpearBackRankKeeps` | true | Step 76 (decision 70, Toni's addition b): a man in rank 1 or deeper of his formation's arrangement (`Agent.GetFormationFileAndRankInfo`, rank 0 = the front / a square's or circle's outer ring) with a man of his own in the rank in front (`LineFormation.GetUnit(file - 1 .. file + 1, rank - 1)` - the staggered line) of a shield wall / square / circle under any order step 41 holds (not a charge, a retreat, no order) keeps the polearm whatever is close - the sidearm's spell cleared, back to the polearm AT ONCE. Advanced. Live. |
| `SpearBackRankInStandingLine` | true | Step 76 (decision 70): the back-rank rule for a plain LINE too while it is on a STOP ("a line held in place"). Advanced. Live. |
| `SpearKeepVsPolearm` | true | Step 76 (decision 70, Toni's addition c): an enemy close who wields a melee polearm himself does not count - a man facing ONLY polearms keeps his (the summary's "facing only polearms"). Off: every close enemy counts (RBM). Advanced. Live. |
| `SpearLookSeconds` | 0.5 | Step 76 (decision 70): RBM's CheckInterval - each man's look every this x 0.75-1.25 (a jitter from his index, no dice). Range 0.1-2. Advanced. Live. |
| `SpearWieldOverride` | false | Step 76 (decision 70): THE WIELD FALLBACK, a test switch for the case the favors alone do not hold - while the polearm is favoured, the AI's own `Wield` event for a SIDEARM slot is turned into the `Wield` of his polearm's slot (step 16's `OnAIInputSet` hook, `SpearMath.WieldRewrite`). Off by default (nothing offline says the favors fail). Advanced. Live. |
| `ThrustsPassFriends` | true | Step 76 (decision 70): A THRUST PASSES A FRIEND - vanilla's `Mission.MeleeHitCallback` cancels a friend's damage (`CancelsDamageAndBlocksAttackBecauseOfNonEnemyCase`), stuns the attacker `StunPeriodAttackerFriendlyFire` (0.4 s) and the damage model's `DecideWeaponCollisionReaction` then BOUNCES the 0-damage blow; our damage decorator answers `MeleeCollisionReaction.ContinueChecking` BEFORE the base model for a collision with a defender who is the attacker's friend (`Agent.IsFriendOf`), not a kick / bash, a THRUST (`StrikeType` thrust) - the weapon carries on and may reach the enemy behind. Swings, kicks and bashes into a friend still bounce. Everyone's thrusts (you too). Gate: `ModEnabled` FIRST, then this. The [summary] "thrusts past friends" line (every friendly contact by verdict, the same man hitting an enemy within 0.5 s, the friend's flinch). Advanced, "Battle pace (AI)". Live. |
| `ThrustsPassPolearmsOnly` | false | Step 76 (decision 70): only a polearm's thrust passes a friend (`WeaponClass` One / TwoHanded / LowGrip polearm). Off = every thrust. Advanced. Live. |
| `FriendlyHitStunSeconds` | 0.1 | Step 76 (decision 70): the game's `StunPeriodAttackerFriendlyFire` (`ManagedParameters`, one global value - vanilla 0.4 s, every friendly touch, swings too) while `ThrustsPassFriends` is on; the game's own value given back at the battle's end, when the switch or the master switch goes off (like step 47's `StunPeriodMax`). Range 0-1. Advanced. Live. |
| `BraceEnabled` | true | Step 23 (Anton): an AI fighter whose bar (points ÷ his full pool) is at or below his formation's order floor stops attacking in melee and only defends, guard up, until he refills to floor + his margin (§2 "Brace by orders"). Ranged attacks go on. AI heroes and riders too, never you. Off = lifted at once. |
| `BraceFloorChargePercent` | 20 | Step 23: the floor under a charge order (charge, charge a target, attack a gate, the AI's tactical charge), % of the full bar. |
| `BraceFloorAdvancePercent` | 40 | Step 23: the floor under an advance order (yours, or an AI formation advancing on a Move order - its behaviour is an advance). |
| `BraceFloorHoldPercent` | 60 | Step 23: the floor under every other order - hold / stop, move, retreat, fall back, follow, no order (no formation: the next row since step 28). |
| `BraceFloorNoFormationPercent` | 20 | Step 28 (Anton, 2026-10-12 - a tournament 1v1 braced on the hold floor 60%, 62% of the time): the floor of a man in NO formation - tournament and arena fighters, duels. Range 0-100. |
| `BraceFloorSpreadPercent` | 10 | Step 28 (Anton): each AI man's own floor = his order's floor ± up to this many points, a uniform roll made once per man and battle, independent of the margin's roll; the slider rescales every roll live; his floor clamped to 0-100 (the target stays floor + his margin). 0 = no spread. Range 0-30. |
| `BraceRecoverPercent` | 20 | Step 23: the margin - a bracing man attacks again at floor + this (capped at the top his wounds let him refill to; the band keeps its width under the cap). |
| `BraceRecoverSpreadPercent` | 5 | Step 23 (Anton: "+- 5% ... per soldier, once rolled on a battle"): each AI man's own margin = `BraceRecoverPercent` ± up to this, a uniform roll kept for the battle; the slider rescales every roll live; never below floor + 1. 0 = no spread. Range 0-50. |
| `BraceWieldShield` | true | Step 23: a man who starts bracing with a shield not in his hand takes it out (a one-handed weapon first if his weapon needs both hands; a ranged weapon in hand is left alone); checked every 1 s while bracing, at most 3 wield calls a brace. At its end his AI picks again. |
| `BraceRaiseShield` | true | Step 23: while bracing with a shield in hand, the shield is held up (`DefendDown` in his input) whenever his own AI holds no guard. Off: the guard only replaces an attack wish (step 16's rule). |
| `SprintEnabled` | true | Step 33 (Anton, 2026-10-12): the SPRINT - every fighter on foot stores seconds of sprint = his CURRENT Athletics points ÷ `SprintPointsPerSecond` (150 / 20 = 7.5 s; step 35: never below `SprintMinSeconds`; a points drop clamps it); you hold `SprintKey`, the AI sprints on a new order (`SprintAiEnabled`); while sprinting his run speed follows the profile (`SprintMaxMultiplier` × the fresh run → `SprintMinMultiplier` × the current run - step 35) and he accelerates in `SprintTopSpeedReachSeconds`, his Athletics does NOT refill; never mounted. Needs Athletics (the store is its points). Off = no sprint, speeds back at once. Live. |
| `SprintKey` | LeftControl | Step 33: the key you hold to sprint, by NAME (Core `KeyNames`: LeftControl, RightControl, LeftAlt, RightAlt, LeftShift, RightShift, CapsLock, Tab, Q, T, Y, H, J, K, L, B, N, M, C, MiddleMouseButton, X1MouseButton, X2MouseButton - the game's `InputKey` names; case, spaces, '-' and '_' ignored, "left ctrl" works). Left Ctrl is free in v1.4.8's fight keys (RESEARCH §N). Read live (`Mission.InputManager.IsKeyDown`), only in a fight or walking about a town / village (2026-10-14), on foot, with the order menu closed. |
| `SprintPointsPerSecond` | 20.0 | Step 33: Athletics points per stored sprint second (range 5-100). |
| `SprintMinSeconds` | 2.0 | Step 35 (Anton, 2026-10-12: a tired man's store collapsed to nothing): the store is never smaller than this - capacity = max(this, points ÷ `SprintPointsPerSecond`); an exhausted man (points 0) still holds 2 s, refilled at the usual rate rule (capacity ÷ `SprintRefillSeconds` × the factors - so slowly when tired). 0 = no floor (step 33: an empty bar cannot sprint). Range 0-5. Live. |
| `SprintMinStartPercent` | 20 | Step 33: a sprint starts only with at least this % of the store filled (no spamming tiny bursts); 0 = any. Range 0-100. |
| `SprintMaxMultiplier` | 1.6 | Step 33: the speed at a sprint's start - also from a partial store (the dodge). Step 35 (Anton, 2026-10-12): × his FRESH (untired) run speed, whatever his Athletics (was × the current, tired run); defaults.json 2.0 since step 35. Range 1-3. |
| `SprintMinMultiplier` | 1.1 | Step 33: the speed at a sprint's end, × his CURRENT (tired) run speed (step 35: the line runs from `SprintMaxMultiplier` × the fresh run down to this × the current run); never above the start speed. Range 1-3. |
| `SprintFullSpeedSharePercent` | 30 | Step 33 (Anton: 30): the first this-% of the sprint's length T (= the seconds stored at its start; the AI: its share) at `SprintMaxMultiplier`, then a straight line to `SprintMinMultiplier` at T. Full 7.5 s: x1.6 until 2.25 s. The clock runs only while moving. Range 0-100. |
| `SprintTopSpeedReachSeconds` | 1.0 | Step 35 (Anton's taps were 0.4-0.8 s, the game's foot acceleration `TopSpeedReachDuration` ~2.3-2.8 s): while a man sprints the stat decorator writes his `TopSpeedReachDuration` (DrivenProperty 86, per agent) = min(the game's value, this); the sprint's start and end recompute him, so the game's value is back when it ends. 0 = the game's own. Range 0-5 (below 0.1 counts as 0.1). Live (the next recompute; a change marks every sprinter for one). |
| `SprintRefillSeconds` | 7.0 | Step 33: empty → full store standing still (or walking) at / above the peak line; never while sprinting. Range 1-120. |
| `SprintRefillMovingFactor` | 0.5 | Step 33: the store's refill × this while moving faster than a walk (effort above `WalkEffortFraction`): 14 s. Range 0-1. |
| `SprintRefillExhaustedFactor` | 0.5 | Step 33: the refill × a line from 1 at the peak line (f = 1) to this at an empty bar (f = 0); multiplies with the moving factor (28 s moving and empty). Range 0-1. |
| `SprintAiEnabled` | true | Step 33: an AI formation's NEW movement order (its kind changes - step 23's read, the AI's advance / tactical charge on a Move included; polled every 0.25 s) makes each of its AI men on foot sprint once, for up to the order's share of his store (step 37: everything he holds above the order's reserve, `SprintAi*KeepPercent`); he stops early when an enemy is within 3 m (plumbing) or the order changes again. Never you or a hero you drive; brace, step back and the pause untouched. |
| `SprintAiChargeKeepPercent` | 0 | Step 36 / 37 (Anton: one number per order, the reserve): on a new CHARGE order (charge, charge a target, attack a gate, the AI's tactical charge) an AI man on foot keeps this % of his FULL store and sprints everything above it (stored − keep x capacity). 0 = all of it; 100 = none. Range 0-100. |
| `SprintAiAdvanceKeepPercent` | 50 | Step 37: the reserve a new ADVANCE order (F1 F4 "engage", the AI's own advance) keeps, % of his FULL store - he sprints everything above it: from 100% 50%, from 60% 10%, at or below 50% none. Range 0-100. |
| `SprintAiMoveKeepPercent` | 80 | Step 37 (Anton: troops moved into position keep their sprint for the real charge): the reserve a new MOVE order (a plain move to a position, F1 F1; an AI general's Move under any behaviour but its advance / charge) keeps, % of his FULL store - from 100% he sprints 20%, from 90% 10%, at or below 80% none. Range 0-100. |
| `SprintOnRout` | true | Step 57 (Anton, 2026-10-18: "when they are broken and begin to rout, use their available sprint for the first get-away"; decision 49; the [summary] "sprint on rout" line - routs seen, started, refused by reason): when an AI man on foot ROUTS - the game's own rout, `MissionBehavior.OnAgentFleeing` (called once per man whose morale broke, right after `CommonAIComponent.Retreat()`; never for a formation's Retreat order) - he sprints his WHOLE store once (`SprintStore.TryStartRout`: no order reserve, no `SprintMinStartPercent` gate, no end within 3 m of an enemy); a running order sprint is handed over. Needs `SprintEnabled` + `SprintAiEnabled` (and the master switch, Athletics). Never the player, never a rider. Live. |
| `SprintChargeIntoContact` | true | Step 64 (decision 56, Anton's looter battles 2026-10-19: "charge sprints 153 men avg 1.39 s; ends: store empty 100, an enemy within 3 m 65" - looters spent their store early and stopped short; 14 of 160 blows were charges): an AI CHARGE order's sprint and a FEVER sprint run INTO contact - no end at 3 m from an enemy (`SprintStore.IntoContact`); ended by his FIRST ATTACK (`AthleticsLogic.AttackBegan` - the blow then carries the charge, step 62's `SprintEndedAt`) or once he has stood still (below 0.5 m/s) `SprintMath.StoppedEndSeconds` 0.5 s in a row after he began to move; advance / move sprints, yours, a sprint back and a rout's unchanged. The [summary] "sprint into contact" line. Off = step 62 (every AI sprint ends at 3 m). Live (from the next sprint). |
| `SprintChargeWaitBelowSeconds` | 3.0 | Step 64: on a NEW charge order an AI man whose sprint (`SprintMath.AiAmount`: above the charge's reserve) would be shorter than this WAITS (`TrackedAgent.ChargeSprintWaiting`) until an enemy is within `SprintChargeWaitMetres` (looked at every 0.25 s by the sprint pass; a new order / a gate drops it), then sprints it (into contact). Why: a looter's 2 s floor at x2 → x1.1 of his run covers ~12-15 m - spent at the order 30 m out it ends short of the wall; a legionary's 6 s store covers ~45 m and still sprints at the order (step 37's arrival as a block). 0 = never wait. Range 0-10. Live. |
| `SprintChargeWaitMetres` | 10.0 | Step 64: a waiting charge sprint starts once an enemy is within this many metres (a 2 s store at ~7 m/s lasts ~14 m - to the line). Range 1-40. Live. |
| `AiAggressionFreshPercent` | 60 | Step 43 (Anton, 2026-10-17: "a man at work doesn't hold a pace until he drops; the more tired he is, the less he swings"): AGGRESSION BY ATHLETICS - every AI man on foot's two melee attack-decision values, `AIAttackOnDecideChance` (the chance to start an attack at a combat decision: the game's 0.144 charge / 0.096 other orders / 0.05 shield wall, square, circle) and `AIAttackOnParryChance` (the riposte, 0.08 − 0.02 × defensiveness), × a share that is this % at a FULL bar and slides in a straight line to `AiAggressionExhaustedPercent` at an empty one; the bar = points ÷ his FULL pool (the brace's reading; a wound's cap is in it - no special case). Written by the stat decorator after the base model (no floor of ours; × step 5e's m too when `AttackRateAiDecisions` is on), in 0.05 steps of the share. Never the player, never riders, never ranged (`AiShootFreq`, the aim). Tournaments and duels too. 100 / 100 = the game's own AI. Range 0-200. Live. |
| `AiAggressionExhaustedPercent` | 20 | Step 43: the share at an EMPTY bar (0.144 → 0.029 on a charge). On top of the pause after each attack (unchanged) - a tired man has two brakes. Range 0-200. Live. |
| `FeverEnabled` | true | Step 43: the FEVER - a NEW charge / advance order on an AI formation (step 33's order poll and kinds: charge, charge a target, attack a gate, the AI's tactical charge; advance, the AI's advance) arms each of its AI men on foot with a store of his current points ÷ `FeverPointsPerSecond` seconds; while armed or burning his share is lifted to `FeverAggressionPercent`, fading back to his tired share (§2 "Aggression and the fever"). A hold / move / retreat / stop / follow order ends it, armed or burning. Never the player, never riders, no formation = none. Off = the bar's share only, fevers end at once. Live. |
| `FeverPointsPerSecond` | 10.0 | Step 43: the store = his CURRENT Athletics points ÷ this, seconds (100 points = 10 s, 80 = 8 s, a tired 40 = 4 s), read at the order. Range 1-50. |
| `FeverAggressionPercent` | 80 | Step 43: the fever's peak share, ABSOLUTE (of the game's values): armed = max(his tired share, this); burning = tired + (this − tired) × left ÷ full, never below tired - it fades to his CURRENT tired share (which keeps sinking as he fights). A man already above it gets nothing from the fever. 0.144 → 0.115 on a charge. Range 0-200. Live. |
| `FeverContactMetres` | 5.0 | Step 43: the burn starts the first time an enemy is within this many metres of him (`GetNearbyEnemyAgentCount`, looked at every ~0.25 s); once burning it burns to empty, contact or not. Also the summary's "in contact". Range 1-20. |
| `FeverShieldDurationMultiplier` | 0.7 | Step 43: while a shield is in his off hand (read live at every look - step 21's weapon read) the store burns 1 ÷ this fast, so a shield man's fever lasts this share. Range 0.1-2. |
| `FeverSprintToClose` | true | Step 43: a feverish man (armed or burning) with sprint stored above his order's reserve (step 37), not sprinting, an enemy between `FeverSprintMinMetres` and `FeverSprintMaxMetres`, rolls every `FeverSprintCheckSeconds` with chance `FeverSprintChancePercent` × fever left ÷ full (full while armed) to start an AI sprint (step 36's `TryStartAi` - everything above the reserve; it ended at 3 m like any AI sprint until step 64 - since then it runs INTO contact while `SprintChargeIntoContact` is on). Needs `SprintEnabled` and `SprintAiEnabled`. Live. |
| `FeverSprintMinMetres` | 3.0 | Step 43: no fever sprint with an enemy closer than this (in melee). Step 64: 4 in defaults.json (config format 12 moves a file's 3 once). Range 0-10. |
| `FeverSprintMaxMetres` | 12.0 | Step 43: no fever sprint without an enemy within this. Step 64 (Anton's looter battles: a fever sprint from 12 m spent a looter's 2 s store before the line): 8 in defaults.json (format 12 moves a file's 12 once). Range 1-40. |
| `FeverSprintCheckSeconds` | 1.0 | Step 43 (Anton: the sprint fades with the fever): seconds between a feverish man's fever-sprint rolls. Range 0.1-10. |
| `FeverSprintChancePercent` | 50 | Step 43: the fever-sprint chance per roll at a full fever; × fever left ÷ full (half the fever, half the chance; empty, none). 0 = never. Range 0-100. |
| `CourageEnabled` | true | Step 49 (Anton, 2026-10-17 night: "AI men are unrealistically brave"; §2 "Courage and cohesion"): every AI man on foot in a formation (not the player, riders, a siege's attackers, men held in a shield wall, busy / detached / routing men; field battles, sieges' defenders, hideouts - no tournaments, arenas, naval) is looked at every ~0.5 s: his side's toughness vs the enemy's within `CourageRadiusMetres`; below the required odds he is TIMID - walks back (far and short of mates) or holds (among them) with tightened behaviour weights. Gate: `ModEnabled` FIRST, then this; works without Athletics (the tiredness term then 0). Off = the game's AI at once (walks stopped, weights given back); the looks still measure. Live. |
| `CourageRadiusMetres` | 6.0 | Step 49: the circle he weighs mates and enemies in (`Mission.GetNearbyAgents`); full weight to half of it, a line to 0 at its edge. Range 2-30. |
| `CourageBaseOddsPercent` | 100 | Step 49: the odds (his side's toughness ÷ the enemy's, him included) a fresh, steady soldier needs; 100 = even. Range 10-500. |
| `CourageArmourWeightPercent` | 200 | Step 49: toughness × (1 + this % × armour / 100), armour = his equipment's head + body + legs + arms (read once). Range 0-500. |
| `CourageLevelWeightPercent` | 100 | Step 49: toughness × (1 + this % × min(level, 60) / 30) - the troop's skill and experience. With the armour: a looter (8, level 5) 1.35, a legionary (110, level 26) 5.97 = 4.4 looters. Range 0-500. |
| `CourageHealthWeightPercent` | 50 | Step 49: toughness × (1 − this % × the health share he lost). Range 0-100. |
| `CourageHeavyWeaponPercent` | 150 | Step 49 (Anton's addition: shock troops): an ENEMY carrying a two-handed weapon or a polearm and no shield (read once from his kit) counts × this %. Range 100-500. |
| `CourageTiredWeightPercent` | 100 | Step 49: required × (1 + this % × (1 − his bar)), bar = points ÷ his FULL pool; 0 while Athletics is off. Range 0-500. |
| `CourageMoraleWeightPercent` | 100 | Step 49: required × (1 + this % × max(0, 50 − morale) / 50) - the game's morale (`Agent.GetMorale`, 0-100). Range 0-500. |
| `CourageShockSeconds` | 6.0 | Step 49 (Anton's addition: SHOCK): landed hits on him within this many seconds count (and live impact outcomes once an impact family applies); 3 = full shock. 0 = no shock. Range 0-60. |
| `CourageShockWeightPercent` | 100 | Step 49: required × (1 + this % × min(shock events, 3) / 3). A shocked man also needs 2 × `CourageRegroupMates` and half `CourageRegroupRadiusMetres` (packs in). Range 0-500. |
| `CourageFeverPercent` | 40 | Step 49: required × (1 − (1 − this %) × the fever's share - 1 armed, left ÷ full burning). Since step 49b it applies only to a SHOCKED man: an unshocked man in a fever presses whatever the odds. Range 0-100. |
| `CourageHeroPercent` | 75 | Step 49: a hero's required odds × this %. Range 10-200. |
| `CourageHysteresisPercent` | 25 | Step 49: brave again only at required × (1 + this %). Range 0-200. |
| `CourageMinTimidSeconds` | 2.0 | Step 49: a timid spell lasts at least this long. Range 0-10. |
| `CourageSurgeChancePercent` | 5 | Step 49b (the manager's review: "a few of them now and then go and hit the wall"): a timid man NOT shocked, with at least `CourageRegroupMates` mates and an enemy within 8 m, rolls this at each look (~0.5 s) × his bar share (Athletics on); a hit = brave for `CourageSurgeSeconds` (normal weights, no walk). 0 = never. Range 0-100. |
| `CourageSurgeSeconds` | 4.0 | Step 49b: a surge's length. Range 0-30. |
| `CourageRegroupRadiusMetres` | 5.0 | Step 49: a timid man farther than this from his target (the bigger group / the formation's centre) walks; within it he has arrived. Range 1-30. |
| `CourageRegroupMates` | 4 | Step 49: a timid man with at least this many mates within the radius holds instead of walking; 0 = never walks. Range 0-30. |
| `CourageFlockRadiusMetres` | 20.0 | Step 49 (Anton's addition: FLOCKING): the walk's target = the centroid of his side's men within this distance when at least `CourageFlockMinMen` are there, else his formation's `CachedAveragePosition`; 0 = always the formation. Range 0-100. |
| `CourageFlockMinMen` | 6 | Step 49: allies within the flock radius that make a group worth running to. Range 1-100. |
| `CourageSprintBack` | true | Step 49: a walking timid man sprints (step 33's AI sprint, the store above `CourageSprintKeepPercent`) when his target is farther than `CourageSprintMetres` or `CourageSprintEnemies` enemies are near; a regroup sprint does not end at 3 m from an enemy. Needs `SprintEnabled` + `SprintAiEnabled`. Live. |
| `CourageSprintMetres` | 12.0 | Step 49: the target farther than this asks for the sprint back. Range 0-100. |
| `CourageSprintEnemies` | 3 | Step 49: this many enemies within the radius ask for the sprint back. Range 1-30. |
| `CourageSprintKeepPercent` | 50 | Step 49: the sprint back spends only the store above this share of his full store (its own reserve, in place of his order's). Range 0-100. |
| `CouragePanicOddsPercent` | 50 | Step 49 (Anton's addition: PANIC): shocked AND odds below this % of the required = panic - he sprints at once whatever the distance. 0 = no panic. Range 0-100. |
| `CouragePanicSprintKeepPercent` | 0 | Step 49: a panic sprint keeps only this share of his store (below his order's reserve). Range 0-100. |
| `CourageTightenWeights` | true | Step 49: while timid his `HumanAIComponent.OverrideBehaviorParams`: Melee 10 → 6.5 at `CourageTimidMeleeMetres` → 0.01 1.5 m further, GoToPos 5 → 6 (7 m) → 6 - re-applied at every look (the game's `RefreshBehaviorValues` resets them on orders); brave again = `Agent.RefreshBehaviorValues(order, arrangement)` (the game's own set). Live. |
| `CourageTimidMeleeMetres` | 2.0 | Step 49: a timid man's reach - his Melee weight stays above his GoToPos up to here. Range 0.5-6. |
| `FearFlockEnabled` | true | Step 67 (Anton 2026-10-19, decision 60): the FEAR FLOCK - each AI man on foot in a formation (courage's looks, every ~0.5 s) gets a FEAR from low morale and lost freshness × (1 − discipline), mixed with his unit's; above `FearStartPercent` his place blends from his slot toward a SHELTER (his unit's centre of mass pushed back from the enemy near the unit), behind a comrade if he can; walked there through courage's input lever (a fifth movement wish `Flock` - courage's walk back wins). Off = slots as the game gives them (measured ON and OFF). Never you, riders, a siege's attackers, no formation, a held shield wall above `FearWallBreakMorale`. Live. |
| `FearMoraleCalm` | 60 | Step 67: at or above this morale (`Agent.GetMorale()`, 0-100) morale adds no fear. Range 0-100. Live. |
| `FearMoraleBroken` | 15 | Step 67: at or below this morale the morale share of fear is 1; a line between. Range 0-100. Live. |
| `FearFreshPercent` | 75 | Step 67: at or above this share of his bar (points ÷ FULL pool) tiredness adds no fear; a line to 1 at an empty bar. Athletics off = 0. Range 1-100. Live. |
| `FearMoraleWeightPercent` | 100 | Step 67: the morale share's weight a in raw fear = 1 − (1 − a·m)(1 − b·t). Range 0-100. Live. |
| `FearTiredWeightPercent` | 100 | Step 67: the tiredness share's weight b in the same. Range 0-100. Live. |
| `DisciplineLevelLow` | 6 | Step 67: discipline 0 at or below this troop level (looters ~5, recruits 6). Range 0-60. Live. |
| `DisciplineLevelHigh` | 26 | Step 67: full discipline at or above this level (an imperial legionary 26); a line between; heroes full. Range 1-62. Live. |
| `DisciplineMaxPercent` | 70 | Step 67: the full discipline d - fear × (1 − d). Range 0-100. Live. |
| `FearUnitSharePercent` | 50 | Step 67: fear = (1 − u) × his own + u × his unit's mean (the last 0.5 s of looks). Range 0-100. Live. |
| `FearThinLinePercent` | 25 | Step 67: + this × (1 − d) to his own fear while an enemy is within 8 m and no comrade stands behind him (within 3 m back, 1.5 m aside of the line to the nearest enemy) - green troops thicken a thin line. Range 0-100. Live. |
| `FearStartPercent` | 15 | Step 67: the flock pull p = clamp((fear − start) ÷ (full − start)); below the start p = 0 - he keeps his slot exactly. Range 0-95. Live. |
| `FearFullPercent` | 80 | Step 67: fear at which p = 1 (his place = the shelter). Range 5-100. Live. |
| `FearShelterBackMetres` | 4.0 | Step 67: the shelter = the unit's centre + (away from the enemy near the unit) × this × the unit's mean pull. Range 0-20. Live. |
| `FearShelterMaxMetres` | 12.0 | Step 67: his place is never farther than this from his slot. Range 1-50. Live. |
| `FearDriftMetresPerMinute` | 6.0 | Step 67: the shelter's BACKWARD motion (away from the enemy) is capped at this speed (forward / sideways free). Range 0-60. Live. |
| `FearDriftHoldPercent` | 25 | Step 67: the cap's share under a hold-type order (stop, move, follow, fall back) or a shield wall / square / circle. Range 0-100. Live. |
| `FearCoverWidthMetres` | 0.9 | Step 67: a comrade COVERS him when he stands on the line to the nearest enemy (0.3 m past him, short of the enemy) at most this far aside. Range 0.3-3. Live. |
| `FearAnchorFreshPercent` | 60 | Step 67: a covering comrade with at least this bar ANCHORS him - he holds his spot for `FearAnchorSeconds` (once per cover); below it the pull goes on. Range 0-100. Live. |
| `FearAnchorSeconds` | 20.0 | Step 67: how long an anchor holds. 0 = never. Range 0-120. Live. |
| `FearSprintMetres` | 8.0 | Step 67: farther than this from his place (or alone - fewer than 2 mates in courage's circle with an enemy in it) he sprints there (courage's `TryStartRegroup`), at most once in 10 s. Range 0-50. Live. |
| `FearSprintChancePercent` | 10 | Step 67: beyond 3 m from his place and not sprinting he rolls this a look to sprint anyway. Range 0-100. Live. |
| `FearSprintKeepPercent` | 50 | Step 67: the flock's sprint keeps this share of his full store. Range 0-100. Live. |
| `FearWallBreakMorale` | 30 | Step 67: a step-41 held man is let go (`WallVerdict.Breaking`) when his unit's mean morale is below this - the wall breaks and joins the flock; at or above, the wall holds and the flock leaves its men alone. 0 = never. Range 0-100. Live. |
| `FearChargeLooseMetres` | 12.0 | Step 67: under a charge (no slots) he is pulled in only beyond (1 − p) × this from the shelter (+ 2 m to start again) and left to his AI within it. Range 2-50. Live. |
| `FearWinCalms` | true | Step 69: WINNING CALMS - his fear × (1 − win), the win from courage's local odds (and the enemy near him shaken); the unit's win mixed in by `FearUnitSharePercent` when better. Off = step 67 exactly. Live. |
| `FearWinEvenOddsPercent` | 100 | Step 69: the local odds (× 100, courage's toughness-weighted sums) where the win starts; below it he is losing - his fear untouched (the shaken term too). Range 0-1000. Live. |
| `FearWinClearOddsPercent` | 200 | Step 69: the local odds (× 100) where the win is 1 - his fear gone; a straight line from the even line. Range 1-1000. Live. |
| `FearWinShakenPercent` | 50 | Step 69: at even odds or better the enemy near him SHAKEN (the toughness-weighted mean of their morale shares on `FearMoraleCalm` / `FearMoraleBroken`; a routing man's morale is spent) adds this × the share: win = 1 − (1 − odds win)(1 − this × shaken). Range 0-100. Live. |
| `FearThreatMetres` | 12.0 | Step 69: no enemy within this many metres = no pull (his fear still measured and shown; a spell ends "no enemy near"). 0 = step 67 (a pull without a threat). Range 0-60. Live. |
| `FearMopUpWinPercent` | 50 | Step 69: a CHARGING unit whose men in contact win at least this on average is mopping up - no loose-mass leash (`FearChargeLooseMetres`), its men go for the kill. 0 = never. Range 0-100. Live. |
| `FearBandEnabled` | true | Step 70 (decision 63): the flock's place is a WIDE BAND along the enemy's front, not step 67's point - each man keeps his place ALONG the front (his slot's lateral, under a charge his own; clamped into the band) and is compressed only in DEPTH (between the band's rear and front); in a file holding more men than the band's ranks the men behind them slide out to the nearest file with room, toward the ends on a tie, and stay there for the spell; "step in behind a comrade" only where his file has room. Off = step 67's point shelter. Live. |
| `FearBandRanksMin` | 4 | Step 70: the band's depth in ranks at a small pull; a straight line to `FearBandRanksMax` at the full pull, rounded (the unit's mean pull). Range 1-20. Live. |
| `FearBandRanksMax` | 6 | Step 70: the band's depth in ranks at the full pull (never below `FearBandRanksMin`). Range 1-20. Live. |
| `FearBandFileMetres` | 1.0 | Step 70: one file's width in the band; the band's width from its men = men ÷ ranks × this; the shape lines' files too. Range 0.5-3. Live. |
| `FearBandRankMetres` | 1.0 | Step 70: one rank's depth in the band; the band's depth = ranks × this; a man's rank in his file = (the file's front-most man − him) ÷ this + 1. Range 0.5-3. Live. |
| `FearBandMaxWidthMetres` | 40.0 | Step 70: the band's width = max(the enemy formation's frontage it faces, men ÷ ranks × `FearBandFileMetres`), never wider than this. Range 5-150. Live. |
| `FearBandGapMetres` | 1.5 | Step 70: the band's front WANTS to stand this far short of the enemy's front (the enemy its men in contact see; else the enemy formation's centre less half its depth; else the men's own front), pushed back `FearShelterBackMetres` × the unit's pull; it moves forward freely and back at step 67's drift cap. Range 0-10. Live. |
| `FearStrikeReachMetres` | 2.5 | Step 70: a flocking man with nobody covering him (the band's front rank) and an enemy within this is LET GO to strike (the flock's input off - his AI steps in and swings), unless he stands more than `FearStrikeLeashMetres` ahead of his place. 0 = never let go. Range 0-6. Live. |
| `FearStrikeLeashMetres` | 2.0 | Step 70: a front man let go to strike is walked back to his place once he stands more than this ahead of it (along the line to the nearest enemy). Range 0.5-10. Live. |
| `LoneRunnerEnabled` | true | Step 70 (decision 64): a LONE RUNNER = an AI man on foot in a formation (never you, never riders) more than `LoneRunnerAheadMetres` ahead of his own unit's front or 1 m past the enemy's front, with fewer than 2 mates within courage's radius and an enemy within 8 m. On: (1) the fever no longer presses for a man ALONE (courage's walk back decides him); (2) a charge's / fever's sprint INTO contact ends when he is alone or past the enemy's front; (3) a lone runner walks back to his group (courage's walk, whatever the odds; his side round the enemy kept for the walk; the fever and a surge / punish step-in never turn him round halfway) unless he chases a BEATEN enemy - his side's local odds at `FearWinClearOddsPercent` or better AND the enemy near him broken (shaken 0.5+ on the fear's morale lines). Gate: `ModEnabled` FIRST. Off = measured only (the [summary] "lone runners" line either way). Live. |
| `LoneRunnerAheadMetres` | 4.0 | Step 70: how far ahead of his own unit's front (the men's mean + √3 σ along the direction to the enemy) a man alone counts as out beyond his line. Range 0-30. Live. |
| `PressDontPass` | true | Step 77 (Toni's battles 2026-10-22 10:2x-10:48: 108 T1 recruits against his 59 T5 shield wall - at contact the AI's tactical charge, the rear ranks walking round his wall's ends and behind it, then through the middle; stand firm off changed nothing; decision 71), PRESS, DON'T PASS: an AI man on foot (both sides, never you, never riders - courage's look) whose unit fights an enemy LINE (`PressMath.LineKind`: a formation of `PressLineMinMen`+ men looked at, not retreating, its men mostly held by step 41, OR a block step 74 holds, OR - under no charge - `PressLineInSlotPercent` of them within 3 m of their slots; a square / circle = a RING) never walks past its front: the line's FRONT SEGMENT (`PressLines`, every 0.5 s: in its facing's frame the median of each man-wide lateral bin's front-most man, its ends the extreme front men ± half `PressManWidthMetres`, its GAPS the uncovered stretches between front men of `PressGapMetres`+, its rear a second face) - within `PressWatchMetres` of it the component of his movement toward it is taken out of his own input (a seventh input wish, `AiInputState.Press` - the CLAMP filters the final vector, whoever wrote it); past it by more than `PressToleranceMetres` he is walked straight back out; behind the whole line walked ROUND its nearest end (`PressRoundEndMetres` beyond it); THROUGH only in a gap, ROUND an end only when his unit spans `PressWrapMarginMetres` more than the line and he has `PressWrapMates` mates within `PressWrapMatesMetres`; a rear-rank man of a unit in contact never more than `PressEndSlackMetres` sideways past the line's end; a broken line (a gap) or his unit winning `PressWinPercent` lets go. Also: the fear band never wider than its enemy line + `PressBandMarginMetres` a side (its surplus deeper); the block hold's sideways room within the enemy line's ends + `PressBlockEndMetres` in contact. His attacks untouched. Gate: `ModEnabled` FIRST, then this. Off = measured only (the `[press]` lines, the [summary] "press, don't pass" lines - crossings by where and cause, ON vs OFF). PLAYER page, "How the AI fights". Live. |
| `RotationEnabled` | true | Step 78 (Toni 2026-10-22, sketch 1-4 - the grind; decision 72): ROTATION - an AI man on foot in contact (an enemy within `RotationContactMetres`) whose bar is at or below `RotationTiredPercent` or his brace floor swaps places with a mate of his unit right behind him (`RotationBehindMetres`, `RotationFileMetres`) who is `RotationFresherPercent` points fresher: the fresh man steps up `RotationBesideMetres` beside him while the tired man HOLDS (attacks held, guard up), then the slots swap (`Formation.SwitchUnitLocations`) and the tired man backs into the fresh man's place facing the enemy - the line never opens. Both sides, never the player, never riders; walls through their slots. Needs Athletics. PLAYER page, "How the AI fights". Live. |
| `RestEnabled` | true | Step 78c (Toni 2026-10-22 after 78b: "only per men rule, if tired - step back and wait to recover"; decision 74 - it REPLACED step 78 / 78b's `LullEnabled` / `LullForPlayerUnits` and the 27 `Lull*` settings): EACH MAN RESTS ON HIS OWN - an AI man on foot at the fight (an enemy within `RestReachMetres`, or pressing an enemy line) whose bar falls below his own TIRED mark (`RestTired*Percent`, rolled each rest) stops pushing and backs out of reach facing the enemy (a walk, at most `RestMaxBackMetres` from his start), guard up (strikes only within `RestDefendMetres`), held there (the clamp - the game's charge never drags him in) until his own READY mark (`RestReady*Percent` by his unit's order, rolled each rest, capped by his wounds); CONTAGION - a mate of his unit within `RestContagionMetres` going back in takes him along at up to `RestContagionMaxPercent` near his mark, none below his tired mark + `RestContagionFloorPoints`. A man in a shield wall rests IN his slot (the leash 0, guard only). No unit-wide rule. Both sides, never the player, never riders. Needs Athletics. PLAYER page, "How the AI fights". Live. |
| `PressToleranceMetres` | 0.5 | Step 77 (decision 71): a man carried more than this past an enemy line's front (in its body or beside its end) is walked straight back out to 0.3 m in front of it (`PressMath.PushToMetres`), facing it. Range 0-3. Advanced, "Courage and cohesion (AI)". Live. |
| `PressWatchMetres` | 4.0 | Step 77 (decision 71): within this of an enemy line's front his movement is watched - the inward clamp bites within 0.6 m of the front at each 0.1 s steer (`PressMath.LookaheadMetres` - a 6 m/s run covers it between two steers). Range 0.5-20. Advanced. Live. |
| `PressGapMetres` | 2.0 | Step 77 (decision 71): a GAP = a stretch of a line's front at least this wide that no front man covers (front men = within `PressFrontDepthMetres` of the front, each covering `PressManWidthMetres`): men may go through it, and a line with one is BROKEN (a man behind it is left alone). Range 0.5-10. Advanced. Live. |
| `PressManWidthMetres` | 1.2 | Step 77 (decision 71): the stretch of the front one front man covers (± half of it) - the lateral bin of the front's median and the gaps' measure. Range 0.4-3. Advanced. Live. |
| `PressFrontDepthMetres` | 1.5 | Step 77 (decision 71): a man within this behind a face's front holds it (a rear man stepping up closes a fallen man's gap); a man more than this behind his OWN unit's front (its 10th percentile across the line's face) is in its rear ranks. Range 0.5-5. Advanced. Live. |
| `PressWrapMarginMetres` | 6.0 | Step 77 (decision 71): his unit may go round a line's ends only when its men span ((q95 − q05) ÷ 0.9 across the line's face) at least this much more than the line's front. Toni's 108 recruits (26.6 m) on his 23 m wall: 3.6 m - no. Range 0-60. Advanced. Live. |
| `PressWrapMates` | 5 | Step 77 (decision 71): ... and he goes round only with at least this many mates within `PressWrapMatesMetres` (courage's scan) - a lone man or a small group never. Range 0-30. Advanced. Live. |
| `PressWrapMatesMetres` | 4.0 | Step 77 (decision 71): the mates' distance for the wrap. Range 1-8 (courage's scan reaches 8 m). Advanced. Live. |
| `PressRoundEndMetres` | 2.0 | Step 77 (decision 71): a man behind a line (not wrapping, the line whole, his side not winning) walks sideways behind it to this beyond its nearest end (+ 1 m, `PressMath.ReturnOvershootMetres`), then forward to 1 m in front of its face. Range 0.5-10. Advanced. Live. |
| `PressWinPercent` | 50.0 | Step 77 (decision 71): his unit's mean win (step 69's, `FearUnits.WinOf`) at this or more = the line is beaten - the press lets go. Range 0-100. Advanced. Live. |
| `PressLineMinMen` | 8 | Step 77 (decision 71): an enemy formation is a line only with at least this many men looked at in a period (courage's looks - men on foot). Range 3-200. Advanced. Live. |
| `PressLineInSlotPercent` | 60.0 | Step 77 (decision 71): a formation neither held by step 41 nor a held block is a line under a hold / move / advance (never a charge) while at least this share of its men read are within 3 m of their slots (and half its men have a slot read). Range 0-100. Advanced. Live. |
| `PressBandMarginMetres` | 1.0 | Step 77 (decision 71): the fear band (step 70) is never wider than its enemy formation's frontage + this on each side; its ranks become enough for all its men in its files (beyond `FearBandRanksMax` - the surplus deeper, never out to the sides). Range 0-10. Advanced. Live. |
| `PressBlockEndMetres` | 0.0 | Step 77 (decision 71): once a held block (step 74) is in contact, its sideways allowance is cut to its enemy line's front ends + this (at least 2 m wide, `PressMath.MinBlockRange`). Range 0-10. Advanced. Live. |
| `PressEndSlackMetres` | 1.0 | Step 77 (decision 71): a rear-rank man of a unit in contact (its front within `PressWatchMetres` of the line) may drift at most this far sideways past the line's end - his outward sideways movement beyond it clamped (not for a wrapping man). Range 0-10. Advanced. Live. |
| `PressWrapAllowed` | false | Step 78b (Toni's 225 v 229 battle 2026-10-22 - the enemy's 69 m of recruits went round both ends of his ~31 m line through step 77's wrap, "they should be like packs of dogs ... they want to stay in their pack"; decision 73): step 77's WRAP (a unit `PressWrapMarginMetres` wider than the line, a man with `PressWrapMates` mates near, may go round its ends) only while this is on. Off (the default): nobody goes round; past an end the front BENDS (`PressWingBendDegrees`). A new key - no migration. Advanced, "Courage and cohesion (AI)". Live. |
| `PressWingBendDegrees` | 30.0 | Step 78b (decision 73): past a line's end + `PressEndSlackMetres` the front a man may not cross bends back toward his side by this angle - at p metres past the end it stands tan(angle) x (p − the slack) in front of the line's front; an overlapping wing is clamped along it (its normal cos into the line + sin outward), slides toward the end and stands off it facing it (a refused flank); a man forward of it walked straight back to it (`PressAction.Wing` / a Push to the bent front); a wing man of a unit in contact is also side-clamped past the end + the slack (like a rear-rank man). A unit ORDERED to a place (its order a Move - the player's or the AI general's) is free beside or behind the line. 0 = step 77's straight front. Range 0-80. Advanced. Live. |
| `RotationTiredPercent` | 35 | Step 78 (decision 72): a front man in contact is TIRED at or below this bar (points ÷ his full pool) - or his brace floor (`RotationUseBraceFloor`), whichever is higher. Range 0-100. Advanced, "Courage and cohesion (AI)". Live. |
| `RotationUseBraceFloor` | true | Step 78: his own brace floor (step 23/28 - charge 20, advance 40, hold 60 ± his roll) counts as tired too - a holding line rotates sooner. Needs `BraceEnabled`. Advanced. Live. |
| `RotationFresherPercent` | 25 | Step 78: the mate behind must have at least this many points more and not be tired himself. Range 5-100. Advanced. Live. |
| `RotationBehindMetres` | 1.5 | Step 78: the mate stands 0.3 m (`RhythmMath.MinBehindMetres`) to this far behind the tired man, toward their own side (the direction to his nearest enemy). Range 0.5-4. Advanced. Live. |
| `RotationFileMetres` | 1.0 | Step 78: ... and at most this far to the side of his file. Range 0.2-3. Advanced. Live. |
| `RotationContactMetres` | 2.5 | Step 78: an enemy within this = in contact (a grind front man; the men counted for a lull). Range 1-6. Advanced. Live. |
| `RotationBesideMetres` | 0.7 | Step 78: the fresh man walks to this far beside the tired man at his depth; BESIDE = no more than 0.4 m behind his depth and within this + 0.6 m sideways - only then the slots swap and the tired man backs off. Range 0.3-2. Advanced. Live. |
| `RotationApproachSeconds` | 3.0 | Step 78: not beside within this = the swap called off (the tired man never left). Range 0.5-10. Advanced. Live. |
| `RotationMaxSeconds` | 6.0 | Step 78: a whole swap at most this long (both let go where they stand). Range 2-20. Advanced. Live. |
| `RotationCooldownSeconds` | 12.0 | Step 78: at most one swap per man in this many seconds (a called-off one too). Range 1-120. Advanced. Live. |
| `RotationRestSeconds` | 6.0 | Step 78: the man who went back is clamped from walking forward past his new place for this long. Range 0-30. Advanced. Live. |
| `RestTiredMinPercent` | 20 | Step 78c: each rest a man rolls his TIRED mark between this and `RestTiredMaxPercent` (% of his full bar) - a wide spread, so men never tire out together. Range 0-100. Advanced. Live. |
| `RestTiredMaxPercent` | 45 | Step 78c: the tired mark's other end (below the min = the min). Range 0-100. Advanced. Live. |
| `RestReadyChargeMinPercent` | 55 | Step 78c: a man of a CHARGING unit rolls his READY mark between this and `RestReadyChargeMaxPercent` each rest - eager, back sooner. Range 0-100. Advanced. Live. |
| `RestReadyChargeMaxPercent` | 95 | Step 78c: its other end. Range 0-100. Advanced. Live. |
| `RestReadyAdvanceMinPercent` | 60 | Step 78c: the ready mark under an ADVANCE (an AI formation's Move under an advance behaviour counts) - a little more breath first. Range 0-100. Advanced. Live. |
| `RestReadyAdvanceMaxPercent` | 95 | Step 78c: its other end. Range 0-100. Advanced. Live. |
| `RestReadyHoldMinPercent` | 65 | Step 78c: the ready mark under a HOLD (stop, move, follow, fall back, a wall) - no hurry, the longest wait. Range 0-100. Advanced. Live. |
| `RestReadyHoldMaxPercent` | 95 | Step 78c: its other end. Range 0-100. Advanced. Live. |
| `RestReachMetres` | 3.0 | Step 78c: OUT OF REACH - a man is at the fight while an enemy stands within this; resting he backs until his nearest enemy is this far (+ 0.3 m). Range 1-8. Advanced. Live. |
| `RestMaxBackMetres` | 4.0 | Step 78c: the WALK-BACK LIMIT - never farther than this from where his rest began (Toni saw 8-20 m walk-offs); if the enemy follows he stands and defends himself. 0 = no walk. Range 0-10. Advanced. Live. |
| `RestDefendMetres` | 2.0 | Step 78c: a resting man's attacks are held (guard up) unless an enemy is within this - then he fights as usual. A wall man resting only guards. Range 0-5. Advanced. Live. |
| `RestContagionMetres` | 3.0 | Step 78c: CONTAGION - a mate of the same unit within this going back in rolls each resting man's chance to go with him. 0 = never. Range 0-10. Advanced. Live. |
| `RestContagionMaxPercent` | 60 | Step 78c: the contagion chance at (and above) his ready mark; a line down to 0 at the floor. Range 0-100. Advanced. Live. |
| `RestContagionFloorPoints` | 10 | Step 78c: no contagion while his bar is below his tired mark + this many points. Range 0-100. Advanced. Live. |
| `RestGapDiagnosticMetres` | 2.0 | Step 78c: THE LOG ONLY - how often a natural gap this wide (the 25th percentile of the 2 m bins along the contact) stood between two engaged fronts, by side; it never triggers anything. Range 0.5-10. Advanced. Live. |
| `WarCriesEnabled` | true | Step 79 (Toni 2026-10-22: battles that sound alive; decision 75): WAR CRIES, SOUND ONLY - `Agent.MakeVoice` (Yell on foot, the game's HorseRally for a rider, as vanilla's own yell picks) and never anything else: no movement, readiness, rest mark, morale or order changes. A rested man going back in by his own ready mark shouts at `WarCryBackInPercent`, a mate going in with him by contagion answers at `WarCryAnswerPercent`; a formation's NEW charge order (yours or the AI general's, read by the order poll) makes `WarCryChargeSharePercent` of its men roar, at most `WarCryChargeMaxMen`, staggered. Rate limits: a man, a unit and the battle. Never the player, the routed / fleeing, the dead. PLAYER page, "How the AI fights". Live. |
| `WarCryBackInPercent` | 35 | Step 79: the chance a rested man going back in by his own ready mark (step 78c) shouts. Range 0-100. Advanced. Live. |
| `WarCryAnswerPercent` | 20 | Step 79: the chance a mate who goes in with him by contagion answers. Range 0-100. Advanced. Live. |
| `WarCryAnswerDelaySeconds` | 1.0 | Step 79: an answer comes 0 to this many seconds after (uniform) - never in sync with the first. Range 0-3. Advanced. Live. |
| `WarCryChargeSharePercent` | 40 | Step 79: on a NEW charge order each man of the unit rolls this to be one of its shouters. Range 0-100. Advanced. Live. |
| `WarCryChargeMaxMen` | 12 | Step 79: a unit's shouters on one charge order at most. 0 = no charge roar. Range 0-60. Advanced. Live. |
| `WarCryChargeStaggerSeconds` | 1.5 | Step 79: each shouter's cry 0 to this many seconds after the order (uniform). Range 0-5. Advanced. Live. |
| `WarCryChargeUnitCooldownSeconds` | 15.0 | Step 79: one charge roar a unit in this many seconds (an AI charge flickering on and off roars once). Range 0-120. Advanced. Live. |
| `WarCryManCooldownSeconds` | 8.0 | Step 79: one cry a man in this many seconds, any cause. Range 0-60. Advanced. Live. |
| `WarCryUnitPerSecond` | 8 | Step 79: one unit's cries in any 1 s at most (a sliding window). Range 1-30. Advanced. Live. |
| `WarCryBattlePerSecond` | 16 | Step 79: the whole battle's cries in any 1 s at most (a sliding window) - never a choir, never a sound-engine flood. Range 1-60. Advanced. Live. |
| `CavalryFlockEnabled` | true | Step 55 (Anton, 2026-10-18; §2 "Cavalry flocking", decision 47): every AI rider whose formation is on a CHARGE (charge / charge to target; both sides; never the player, a busy / detached / routing rider; no tournaments, arenas, naval) is in a census every 0.5 s: eager = FREE (vanilla), below the free line he FLOCKS with his kind and his flock FLIPS into a wave. Gate: `ModEnabled` FIRST, then this; works without courage and without Athletics (then everyone is eager - only the timid flock). Off = every flocker let go at once (the scripted walk off, the game's weights back); the census still measures. Live. |
| `CavalryFlockFreeEagerPercent` | 75 | Step 55: a rider's EAGERNESS = his bar (points ÷ FULL pool; 1 with Athletics off) × 0.5 when timid; at or above this % he is FREE (the vanilla dance). Range 10-100. |
| `CavalryFlockHysteresisPercent` | 5 | Step 55: a flocker is free again only at free + this. Range 0-30. |
| `CavalryFlockMidEagerPercent` | 40 | Step 55: the group-size curve's middle point (eagerness %). Range 5-95 (below the free line). |
| `CavalryFlockMidGroupSize` | 4 | Step 55: riders of his kind (him included) a rider at the middle eagerness wants with him before he attacks; 1 at the free line, a line between. Range 1-20. |
| `CavalryFlockExhaustedSharePercent` | 65 | Step 55: at eagerness 0 he wants this % of his kind's riders in his formation (never fewer than the middle size); a line from the middle point; rounded up; never more than his kind has flocking. Range 0-100. |
| `CavalryFlockTimid` | true | Step 55: a rider whose courage odds (step 49's toughness sums within `CavalryFlockTimidRadiusMetres`, the required odds with tiredness / morale / shock / hero) are below what he needs counts at half his eagerness. Needs `CourageEnabled`. Live. |
| `CavalryFlockTimidRadiusMetres` | 15.0 | Step 55: the rider's courage circle (one proximity query at most every 1 s). Range 5-40. |
| `CavalryFlockGatherRadiusMetres` | 25.0 | Step 55: the flocking riders of his kind within this distance are his GROUP ("with me"; a hash grid of this cell size). Range 5-100. |
| `CavalryFlockSeparationMetres` | 3.5 | Step 55: each flocker's own sunflower slot around the group's centre, about this far from the next (fixed for his spell). Range 1-10. |
| `CavalryFlockLeadSeconds` | 2.0 | Step 55: a flocker rides to his group's centre + the heading × the group's speed (at least 4 m/s) × this + his slot - the flock never sits. Range 0-5. |
| `CavalryFlockTightenWeights` | true | Step 55: while flocking his `OverrideBehaviorParams` ChargeHorseback 2 → 0.5 (4 m) → 0.01 (15 m), RangedHorseback 1 → 0.5 (15 m) → 0.1 (30 m) - re-applied every census (the game resets them on orders); the game's back with `Agent.RefreshBehaviorValues`. Live. |
| `CavalryFlockWaveSeconds` | 10.0 | Step 55: after a FLIP (or a give-up) the riders charge as the game's own for this long, then decide again by their eagerness. Range 0-60. |
| `CavalryFlockMaxSeconds` | 45.0 | Step 55: a flocker who never flips is released after this long (a wave of one). Range 5-180. |
| `ImpactEnabled` | true | Step 48, IMPACT I2 (docs/IMPACT.md, §2 "Impact" below): the impact system - kicks / bashes, blocks and clean hits may stagger, drop a shield, knock down, by the PRESSURE x on the man who takes the blow (Core `ImpactMath`). Gate: `ModEnabled` FIRST, `AthleticsEnabled`, this. Live. |
| `ImpactKicksEnabled` | true | Step 48: the kick / bash family (knockdown, shield drop, the longer reel, the slower get-up). Live. |
| `ImpactBlocksEnabled` | true | Step 48: the block family - right-side shield block, weapon parry, wrong-side shield block (stagger, knockdown, wrong-side shield drop, the blocker's stun by tiredness). Step 51: LIVE (off with the riposte on = the riposte alone). Live. |
| `ImpactHitsEnabled` | true | Step 48: the clean-hit family (a longer flinch, a knockdown on a landed weapon blow; the reeling bonus). Live. |
| `ImpactRiposteEnabled` | true | Step 48: the riposte - a fresh defender's right-side block or parry staggers a tired, outmatched attacker (IMPACT §3.6). Step 51: LIVE - a forced stagger of the attacker from the tick. Live. |
| `ImpactShadowMode` | false | Step 48: true = decide and LOG every event, change nothing. Default false - Claude's call 2026-10-17 night (decision 40; IMPACT §7 planned true until I3): Anton could not playtest the spike first and wants to see it live in the morning; every family keeps its own switch and this one turns ALL effects into logging only. The spike (`ImpactSpike` on) forces shadow. Live. |
| `KickShieldDropMaxPercent` | 90 | Step 48: the kick / bash row's shield-drop ceiling (Anton's anchor). Range 0-100. |
| `KickKnockdownMaxPercent` | 80 | Step 48: the kick / bash row's knockdown ceiling (Anton's anchor). Range 0-100. |
| `RightBlockStaggerMaxPercent` | 80 | Step 48: the right-side shield block's stagger ceiling (Anton's anchor). Step 56 (decision 48): 30 in defaults.json - a correct block breaks a man only on a big mismatch (config format 10 moves an old 80 once). Range 0-100. |
| `RightBlockShieldDropMaxPercent` | 0 | Step 48: the right-side shield block's shield-drop ceiling - 0 (IMPACT §8 q7: a clean block is the reward for skill). Range 0-100. |
| `RightBlockKnockdownMaxPercent` | 40 | Step 48: the right-side shield block's knockdown ceiling. Step 56 (decision 48): 10 in defaults.json (format 10 moves an old 40 once). Range 0-100. |
| `ParryStaggerMaxPercent` | 80 | Step 48: the weapon parry's stagger ceiling. Step 56 (decision 48): 30 in defaults.json - a parry is a correct block (format 10 moves an old 80 once). Range 0-100. |
| `ParryKnockdownMaxPercent` | 40 | Step 48: the weapon parry's knockdown ceiling. Step 56 (decision 48): 10 in defaults.json (format 10 moves an old 40 once). Range 0-100. |
| `WrongBlockStaggerMaxPercent` | 90 | Step 48: the wrong-side shield block's stagger ceiling. Range 0-100. |
| `WrongBlockShieldDropMaxPercent` | 50 | Step 48: the wrong-side shield block's shield-drop ceiling. Range 0-100. |
| `WrongBlockKnockdownMaxPercent` | 40 | Step 48: the wrong-side shield block's knockdown ceiling. Range 0-100. |
| `HitStaggerMaxPercent` | 80 | Step 48: the clean hit's stagger (a lengthened flinch) ceiling. Range 0-100. |
| `HitKnockdownMaxPercent` | 50 | Step 48: the clean hit's knockdown ceiling. Range 0-100. |
| `RiposteStaggerMaxPercent` | 50 | Step 48: the riposte ceiling - × S(x_r) × the DEFENDER's f (IMPACT §3.6, §8 q8). Step 56 (decision 48): 70 in defaults.json (config format 10 moves an old 50 once), × (1 − `RiposteBlockerTiredPercent` × T_D) in place of × f_D. Range 0-100. |
| `CorrectBlockTiredWeightPercent` | 25 | Step 56 (decision 48, Anton 2026-10-18: "right blocks a possible penalty for the ATTACKER"): on a CORRECT block (right-side shield, parry) the blocker's T enters x × this % - x = this × T_D + the mismatch (+ reeling, damage); a wrong-side block, a kick and a clean hit keep T in full. 100 = step 48. Range 0-100. |
| `RiposteBlockerTiredPercent` | 0 | Step 56 (decision 48): the riposte's chance × (1 − this % × T_D) - 0 = the blocker's own tiredness does not save a tired attacker; 100 = step 48's × f_D. Range 0-100. |
| `StaggerBaseSeconds` | 0.6 | Step 48: a stagger's length at T 0, s (IMPACT §3.7). Step 51: live - the forced stagger played at the speed for it. Range 0.1-3. |
| `StaggerTiredExtraPercent` | 150 | Step 48: the stagger × (1 + this % × T) - 0.6 → 1.5 s empty. Range 0-500. |
| `StaggerMaxSeconds` | 2.0 | Step 48: no stagger longer, whatever the roll. Range 0.2-5. |
| `KickReelTiredExtraPercent` | 150 | Step 48: the kicked man's vanilla reel × (1 + this % × T) (× 2.5 empty). Step 50: live - his reel played at 1 ÷ that factor, measured. Range 0-500. |
| `GetUpMaxSlowdown` | 2.0 | Step 48: the knocked-down man's fall-and-rise × (1 + (this − 1) × T), never above this. Step 50: live for a kick's knockdown - played at 1 ÷ that factor, measured. Range 1-4. |
| `BlockStunTiredExtraPercent` | 100 | Step 48: every block's defender stun × (1 + this % × T). Step 51: live - the decorator's stun seam, no die (the game's stun held at its own cap first). Step 56 (decision 48): 0 in defaults.json - the spike's lever 2 FAILED (the longer stun changes nothing the engine plays); the seam stays, factor 1 = the game's stun (format 10 moves an old 100 once). Range 0-500. |
| `RiposteStunMaxSeconds` | 1.0 | Step 48: the riposted attacker's recoil = this × (0.5 + 0.5 T_A), s, never above `ImpactStunCapSeconds`. Step 51: live - the length of the attacker's forced stagger. Range 0-3. |
| `ImpactRandomPercent` | 30 | Step 48: every length × a roll in [1 − r, 1 + r], r = this % × T (none in the green). Range 0-100. |
| `ImpactStunCapSeconds` | 2.0 | Step 48: the game's `StunPeriodMax` 0.8 raised to this while the system lengthens stuns (every other stun clamped to the game's 0.8). Step 51: raised while blocks are live, given back when they stop. Range 0.8-5. |
| `ImpactStaggerImmunitySeconds` | 2.0 | Step 48: no new stagger on a man until this long after his impact stagger (or lengthened reel) ends (IMPACT §3.9). Range 0-20. |
| `ImpactKnockdownImmunitySeconds` | 5.0 | Step 48: no new knockdown until this long after he is up. Range 0-30. |
| `ImpactDiminishWindowSeconds` | 8.0 | Step 48: each outcome he suffered within this window multiplies the next chance against him by `ImpactDiminishFactor`. 0 = never. Range 0-60. |
| `ImpactDiminishFactor` | 0.5 | Step 48: the factor per recent outcome (0.5^n). 1 = no limit. Range 0-1. |
| `ShieldPickupLockSeconds` | 0.0 | Step 48: a dropped shield cannot be picked up for this long; 0 = vanilla's pickup (IMPACT §8 q3). Step 50: applied to the AI through a decorator of the game's `ItemPickupModel` (the player's use key is never locked). Range 0-120. |
| `ImpactOnPlayerPercent` | 100 | Step 48: every chance against YOU × this % (IMPACT §8 q5). Range 0-100. |
| `ImpactMeetRulesEnabled` | true | Step 82 (decision 78, Toni 2026-10-22): THE MEET RULES - a kick / bash is judged by the guard that met it at contact (`ImpactMeetMath.Classify`): a bash met by a guard RIGHT or a kick met by a guard DOWN = the ATTACKER at risk (the rows below, the riposte's pressure), the defender holds; any other guard = the defender's rows; the AI reads an incoming kick / bash. The PLAYER page ("Staggers and knockdowns"). Off = step 65 exactly (a bash into a raised shield decides nothing). Live. |
| `ImpactRiderDismountPercent` | 50 | Step 48: a rider's "knockdown" = knocked off at this % of the knockdown chance; never a stagger on a rider (IMPACT §3.8, §8 q4). Range 0-100. |
| `ImpactTiredNeverShrugsOff` | true | Step 48: a tired man's flinch threshold × f - he shrugs off less (I5). Step 52: applied in the damage decorator's `DecideAgentShrugOffBlow` (main thread) to a clean hit on a man on foot while clean hits are live - the game's threshold × (1 − T), T × `ImpactOnPlayerPercent` for you. Live. |
| `ImpactMomentumEnabled` | true | Step 52, MOMENTUM (Anton 2026-10-17: a fresh shock unit charging a tired line must be devastating): the attacker's own speed into the blow adds impact - kicks, bashes and weapon blows, blocked or not; a rider's = his horse's speed at the rider settings (step 53, below). Live. |
| `ImpactMomentumPercent` | 60 | Step 52: the impact added at a full sprint - I × (1 + this % × clamp((v − walk) ÷ (sprint − walk), 0, 1)), v = his velocity along the line to the man he strikes. Range 0-200. |
| `ImpactMomentumWalkMetresPerSecond` | 2.0 | Step 52: up to this speed into the blow nothing is added (the game's walk limit measured 1.8 m/s). Range 0-10. |
| `ImpactMomentumSprintMetresPerSecond` | 7.0 | Step 52: from this speed the whole bonus is added (a run measured ~4.4 m/s, a sprint x1.8-2 of a fresh run). Range 0.5-20. |
| `ImpactMomentumRiderPercent` | 30 | Step 53 (decision 45): a rider's active blow (swing or thrust - never a couched lance, the game's own big hit) carries his HORSE's speed into the blow (vanilla's own rider velocity, along the line to the man he strikes): I × (1 + this % × clamp((v − walk) ÷ (gallop − walk), 0, 1)) - half a man's sprint, so a cavalry charge throws tired men down without felling a line. Range 0-200. Live. |
| `ImpactMomentumRiderWalkMetresPerSecond` | 3.0 | Step 53: up to this horse speed into the blow a rider adds nothing (a walk; a trot ~4-5 m/s). Range 0-20. |
| `ImpactMomentumRiderGallopMetresPerSecond` | 10.0 | Step 53: from this horse speed the whole rider bonus is added (the game's mount speed = 0.22 × (1 + the horse's speed stat): a 45-speed horse ~10 m/s). Range 0.5-30. |
| `ImpactShowMessages` | true | Step 53 (IMPACT §8 q12): a short line in the game's message feed for what happens TO you (knocked down, knocked off your horse, your shield knocked away, reeling from a riposte) and what YOU do (knock a man down / off his horse, break his guard, knock his shield away) - only what was APPLIED (never in shadow mode), rate-limited and merged. The game's colours: done to you #D65252, done by you #AE4AD9, your men white. Live. |
| `ImpactMessagesIncludeYourMen` | false | Step 53: also your side's NOTABLE moments - a man of yours knocks an enemy hero down or off his horse, a hero of yours is knocked down or off his horse. Live. |
| `ImpactMessageMinSeconds` | 1.5 | Step 53: at most one such line every this many seconds; what happens meanwhile is merged into the next (at most 3 parts, "(+N more)"). 0 = a line each tick that has something. Range 0-10. Live. |
| `ImpactPunishEnabled` | true | Step 54 (IMPACT I7, §8 q14, decision 46): THE AI PUNISHES A WINDOW - an AI man on foot whose target (`GetTargetAgent()`, else his immediate enemy) is REELING or DOWN from a live impact outcome (his `ImpactState`; the player's too) decides to attack at `ImpactPunishAggressionPercent` for the window + `ImpactPunishGraceSeconds`; a timid man within `ImpactPunishRangeMetres` steps in like a surge. Both sides, never the player as the attacker, never a rider, never in shadow. Looked at with the fever's look (~0.25 s). Live. |
| `ImpactPunishAggressionPercent` | 100 | Step 54: the aggression share during a window, ABSOLUTE like the fever's peak (100 = the game's own AI): S = max(his share - tired / fever -, this), 0.05 steps through step 43's recompute path. Range 0-200. Live. |
| `ImpactPunishGraceSeconds` | 0.5 | Step 54: the window = until the end of his reeling / time on the ground + this. Range 0-3. Live. |
| `ImpactPunishRangeMetres` | 3.0 | Step 54: a TIMID man (step 49) presses a window only when the target is within this many metres - brave until it closes (a surge); 0 = never. Range 0-10. Live. |
| `ImpactShrugOffYieldsToImpact` | true | Step 62 (decision 52), THE LOST FALLS: the engine asks `DecideAgentShrugOffBlow` FIRST in `Mission.CreateMeleeBlow` and a weapon blow flagged ShrugOff plays no reaction - on: when the game says "shrugged off", the model decides the blow THERE and refuses the shrug-off if it knocks him down or staggers him (the KnockDown seam takes the same decision - one per blow); off = the game's shrug-off wins (steps 52-61). Live. |
| `ImpactFallBackupSeconds` | 0.5 | Step 62: a knockdown decided on a LANDED blow (kick, bash, clean hit) whose Fall has not shown within this many seconds = our own knockdown blow (lever 6: KnockDown + KnockBack, 1 damage, owned by the attacker) from the tick, once; its Fall watched like the game's. 0 = no backup. Range 0-3. Live. |
| `ImpactToughnessWeight` | 0.5 | Step 62: TOUGHNESS in the mismatch - R × (toughness_A ÷ toughness_D)^this, toughness = courage's measure (step 49: (1 + `CourageArmourWeightPercent` × armour ÷ 100) × (1 + `CourageLevelWeightPercent` × min(level, 60) ÷ 30) × (1 − `CourageHealthWeightPercent` × the health lost), the kit read once a man); the riposte turned round. 0.5: a legionary (5.97) on a looter (1.35, 4.4x) x2.10 - strongly, not absolutely. 0 = off. Range 0-2. Live. |
| `ImpactRangedInHandPercent` | 130 | Step 62: a LANDED blow (kick, bash, clean hit) on a man holding a bow, crossbow or sling (not a thrown weapon) - R × this %: both hands busy, no guard. 100 = off. Range 100-300. Live. |
| `ImpactRearPercent` | 200 | Step 62, DIRECTION: a LANDED blow (kick, bash, clean hit) from BEHIND - the angle between the defender's facing (`GetMovementDirection`) and the line from him to the attacker above `ImpactRearDegrees` - R × this % (a doubling: +0.25 x). Blocks never (a blocked blow met the guard; a blow from behind is never blocked). 100 = off. Range 100-500. Live. |
| `ImpactFlankPercent` | 140 | Step 62: a landed blow from the SIDE (`ImpactFlankDegrees` to `ImpactRearDegrees`) - R × this %. 100 = off. Range 100-500. Live. |
| `ImpactRearDegrees` | 120 | Step 62: "from behind" = more than this many degrees off his facing. Range 90-180. Live. |
| `ImpactFlankDegrees` | 60 | Step 62: "from the side" = from this many degrees off his facing (never above `ImpactRearDegrees`). Range 0-180. Live. |
| `ImpactRearPressure` | 0.5 | Step 63 (decision 53), A STRONG REAR BLOW: a LANDED blow (kick, bash, clean hit) from BEHIND adds this to the pressure x STRAIGHT - like tiredness (T is 0-1 in x): a fresh even man struck in the back is pressed like a half-exhausted one (down 7%, staggered 40%). Blocks never. With it `ImpactRearPercent` / `ImpactFlankPercent` ship at 100 (defaults.json; format 11 moves 200 / 140 once) - both would count the direction twice. 0 = off. Range 0-2. Live. |
| `ImpactFlankPressure` | 0.2 | Step 63: a landed blow from the SIDE adds this to x (a fresh even man staggered 8%, never down). 0 = off. Range 0-2. Live. |
| `ImpactChargeCarrySeconds` | 1.5 | Step 62, THE CHARGE: a man on foot is CHARGING while his sprint (step 33's store; never a sprint back or a rout) runs, and for the first blow he strikes within this many seconds after it ended (an AI sprint ends 3 m from the enemy) - his speed into the blow counts at least `ImpactMomentumSprintMetresPerSecond` (the full momentum), the reflected charge and the full critical read it. 0 = only while sprinting. Step 64 (decision 56): 3.0 in defaults.json (config format 12 moves a file's 1.5 once), and an attack he BEGAN while charging (sprinting or within this) stays a charge until it ends or its blow is decided (`TrackedAgent.ChargeAttack`, marked at `AttackBegan` before a sprint into contact ends there) - the carry reaches a slow swing's blow. A RUNNING (not charging) blow's momentum and reflected charge already scale with its speed (`MomentumShare`, 2 → 7 m/s). Range 0-5. Live. |
| `RiposteKnockdownMaxPercent` | 40 | Step 62, THE REFLECTED CHARGE: on a RIGHT-SIDE SHIELD block (never a parry) the CHARGING attacker is THROWN BACK (our own knockdown blow on him, lever 6, owned by the blocker, from the tick) at this % × S((x_r − `RiposteKnockdownFrom`) ÷ (1 − it)) × his charge's share (0 at a walk, 1 at the sprint reference) × the riposte's blocker / diminish / player factors; rolled BEFORE the riposte's reel, which it replaces. 0 = never. Range 0-100. Live. |
| `RiposteKnockdownFrom` | 0.0 | Step 62: where the thrown-back curve starts on x_r (0 = like the riposte's reel: any mismatch against the charger counts - the charge share gates it, a walking man is never thrown). Range 0-0.95. Live. |
| `RiposteMomentumPercent` | 100 | Step 62: on a correct block (right-side shield, parry) the attacker's momentum turns AGAINST him - R_r uses his blow's own impact (no momentum) × (1 + this % × his charge's share): at a walk step 52's riposte exactly. 0 = his speed does not count. Range 0-300. Live. |
| `ImpactMultiplierCapPercent` | 800 | Step 62, THE CAP: momentum × toughness × ranged × direction on R (the riposte: its reflected momentum ÷ toughness) together clamped to [1 ÷ this, this] (800 = x8: a sprinting legionary into a peasant archer's back reaches x7.6 - under it; a lord's charge on a looter archer's back x12 - held at 8, -0.16 x). Range 100-2000. Live. |
| `AiKicksEnabled` | true | Step 65 (decision 57, Anton 2026-10-19: "make the AI do a few shield bashes and kicks now and then - the game has it but the NPCs never use them"): THE AI KICKS AND BASHES - every AI man on foot is looked at about every 0.25 s (staggered); in reach of the enemy he fights (`GetTargetAgent()`, else his immediate enemy - on foot, in front) he kicks (no shield) or shield-bashes at `AiKicksPerMinute` × his skill × his bar × the turtle × the fever, through step 16's input hook: the engine's kick key (`EventControlFlag.Kick`) - with the guard up (`DefendUp`, 0.4 s first) it is a bash - then his own input held off until it plays out (≤ 1.5 s). Never you, a rider, a man staggered / down / routing / with a ranged weapon / without the Athletics or a stored swing for it; `AiKickCooldownSeconds` between two. The cost = a kick's (`CostPerKickOrBash` + a stored swing, seen by step 18's poll). Needs `AthleticsEnabled` (the cost and the sight of the action). The [summary] "ai kicks" lines: asked → played → landed by kind and reason. Live. Step 81 (decision 77, Toni 2026-10-22: "since they add good disturbance to the battle and also dont do much damage I want to try increase them"): no rate any more - each melee move an AI man on foot is about to make (his WIND-UP, seen by step 13's phases, rolled by the tick) with the enemy he fights within `AiKickReachMetres` in front of him is ROLLED by the shares `AiShieldmanKickPercent` / `AiShieldmanBashPercent` / `AiNoShieldKickPercent` - a rolled kick / bash TAKES THE PLACE of that swing (the attempt's guard cancels the wind-up; a kick's guard 0.15 s); refused (he swings) when staggered / down / his kick still playing / without the Athletics / the pool full, with no stored swing, within `AiKickCooldownSeconds`, or off his strike path (step 80). Beyond kick reach nothing is rolled - a spearman keeps his ground. Steps 65 / 66's rate, its skill / bar / turtle / fever factors, the bar gate (none below 35%), the wait for reach and the bash credit are retired (14 keys - config format 19 drops them). |
| `AiShieldmanKickPercent` | 20 | Step 81 (decision 77): of the melee moves an AI man WITH a shield makes with the enemy in kick reach in front of him, this share are KICKS (`AiKickMath.Roll`: the die below it). With `AiShieldmanBashPercent` never over 100 - set over, the bash share is cut to 100 − this (`AiKickRules.ShieldSharesCut`, said in the describe line). Range 0-100. Live. |
| `AiShieldmanBashPercent` | 35 | Step 81 (decision 77): of those moves of an AI man WITH a shield, this share are SHIELD BASHES (the die from the kick share to kick + bash); he swings the rest (45 by default). A rolled bash into a raised shield facing him is a kick (`AiKickKicksARaisedShield`). Range 0-100. Live. |
| `AiNoShieldKickPercent` | 35 | Step 81 (decision 77): of the melee moves an AI man WITHOUT a shield makes with the enemy in kick reach in front of him, this share are KICKS; he swings the rest (Toni: "this will make shockers throw people around more"). Range 0-100. Live. |
| `AiKickReachMetres` | 1.6 | Step 65: the enemy he fights within this many metres (a kick reaches about a pace; RBM 1.4). Range 0.5-4. Live. |
| `AiKickFacingDegrees` | 40 | Step 65: the enemy within this many degrees of his facing (step 62's angle). Range 0-180. Live. |
| `AiKickCooldownSeconds` | 10.0 | Step 65: at least this long between two attempts of one man (from the ask). Range 0-60. Live. Step 66: 8 in defaults.json (config format 13 moves a file's 10 once); after a KICK only - a bash has `AiBashCooldownSeconds`. Step 81: THE minimum between two kicks OR bashes of one man (from the ask) - step 66's separate `AiBashCooldownSeconds` retired; 2 in defaults.json (config format 19 moves a format 13-18 file's 8 once) - a safety, not the frequency (the shares are). |
| `AiKickKicksARaisedShield` | true | Step 65 (Claude's call, RBM's finding): a raised shield stops a bash, not a kick - a shieldman whose target holds his shield up (`DefendShield`) facing him within 60° KICKS instead of bashing. Off: a man with a shield always bashes. Live. |
| `KickBraceEnabled` | true | Step 65 (decision 58, Anton: "make sure a looter kicking a heavy shielded troop doesn't do too much - maybe now and then a lucky kick can stagger him for a fast bit"): THE KICKED MAN BRACES - the impact model's decision of a landed kick / bash on a man on foot asks a brace first: failure chance S((x − `KickBraceFromPressure`) ÷ (`KickBraceToPressure` − from)) on the kick's own pressure x (T_D, the mismatch with step 62's toughness, skill, a bash's shield weight, reeling, the direction); a SPENT man (below `KickBraceMinBarPercent`) never braces. BRACED: no knockdown, no shield drop (refused "braced"), the game's knock-back flag refused in the damage decorator (`KickBraceRefusesKnockBack`), what reaction the engine still plays (the blow is NoIgnore) at `KickBraceReelPercent` of its length. FAILED: step 50's rolls, the reel × (`KickShortReelPercent` + (1 − it) × T_D) - short on a fresh man, the full tired reel on an empty one. You and the AI alike (your failure × `ImpactOnPlayerPercent`). The [summary] "kick brace" lines; the on-screen line for yours. Live. |
| `KickBraceFromPressure` | -0.7 | Step 65: below this pressure x the brace never fails (an S-curve to the next). -0.7: a fresh looter's kick on a fresh legionary (x −0.32) fails 13%. Range -3 to 2. Live. |
| `KickBraceToPressure` | 1.0 | Step 65: at or above this pressure the brace always fails (an empty man: x ≥ 1). Range -2 to 3. Live. |
| `KickBraceMinBarPercent` | 20 | Step 65: below this % of his bar no brace at all - step 50 in full. Range 0-100. Live. |
| `KickBraceReelPercent` | 20 | Step 65: a braced man's leftover reaction plays at this % of its length (5 x its speed; never faster than 8 x). Range 5-100. Live. |
| `KickShortReelPercent` | 40 | Step 65: a failed brace's reel on a FRESH man, % of the game's knock-back (a lucky kick, "a fast bit"); × (this + (1 − this) × T_D) on step 50's reel. Range 5-100. Live. |
| `KickBraceRefusesKnockBack` | true | Step 65: a braced man's knock-back flag refused (`DecideAgentKnockedBackByBlow` - the damage decorator asks `IKnockBackDecider`). Off: the game's knock-back plays, only sped up. Live. |
| `AiKickFriendClearMetres` | 0.5 | Step 68 (decision 61, Toni's 100 v 100 of 2026-10-19: 92 AI kicks / bashes "landed on the man" but the impact model decided 33; the looter battle 32 / 6 - a kick or bash that lands on a FRIEND is cancelled by the game and never decided, step 63): an AI man does not kick / bash the enemy he fights while one of his own side stands between them within this many metres of the line (`AiKickMath.FriendInPath`: more than 0.2 m past him, short of the enemy; the game's proximity search, read only in reach and in front) - the decision waits for a clear line like one out of reach (`AiKickSkip.FriendInTheWay`). 0 = no check (step 66). The "ai kicks" line says whom the landed ones hit (his target / another enemy / a FRIEND / a horse or untracked); the "impact kicks / bashes landed vs decided" line every landed kick against the decisions. Step 80 (decision 76, Toni's logs: 314 of 570 landed AI kicks / bashes on a FRIEND at 17:18, 427 of 668 at 16:17 - the kick goes along his FACING, step 68 measured the line to the enemy): the key now = the KICK's STRIKE PATH half-width, measured from the line of his facing (`AiKickPathProbe`): the enemy must be IN the path (0 < along ≤ `AiKickReachMetres`, ≤ this off its line) and no man of his side in it closer (more than 0.2 m ahead, short of the enemy, ≤ this off the line) - else the decision waits (`AiKickSkip.TargetOffPath` / `FriendInTheWay`); 0.6 in defaults.json (config format 18 moves a format 14-17 file's 0.5 once). Range 0-2. Live. |
| `AiBashPathHalfWidthMetres` | 0.75 | Step 80 (decision 76): a shield BASH's strike path half-width from the line of his facing (the shield is wider than a foot) - the same rule as `AiKickFriendClearMetres` for bashes; the bash's path is read again when its 0.4 s guard ends, just before the key (not clear = the attempt ends, `AiKickEnd.PathBlocked`). 0 = no check for bashes. Range 0-2. Live. |
| `AiKickRearRankMetres` | 1.2 | Step 80 (decision 76): a friend first in his kick / bash path within this many metres of him = a REAR-RANK man: refused with no wait (`AiKickSkip.BehindFriend`) - a friend first farther off only makes the decision wait. 0 = a friend first always waits. Range 0-3. Live. |
| `MeetBashShieldRightStaggerPercent` | 70 | Step 82 (decision 78): a bash met by a raised shield guarding RIGHT: the BASHER's stagger ceiling (S on x_r from `ImpactStaggerFrom`, the riposte's forced stagger from the tick, its length §3.7's on HIS tiredness); the blocker takes nothing. Range 0-100. Live. |
| `MeetBashShieldRightFallPercent` | 30 | Step 82 (decision 78): the same correct meet: the BASHER's fall ceiling (S on x_r from `ImpactKnockdownFrom`; our own blow on him from the tick, step 62's thrown-back lever; a fall replaces the stagger). Range 0-100. Live. |
| `MeetBashWeaponRightStaggerPercent` | 45 | Step 82 (decision 78): a bash met by a weapon guard RIGHT: the BASHER's stagger ceiling; the defender holds (the engine lands it - his knock-back refused in the knock-back seam, the reaction cut to `KickBraceReelPercent`). Range 0-100. Live. |
| `MeetBashWeaponRightFallPercent` | 15 | Step 82 (decision 78): the same correct meet: the BASHER's fall ceiling. Range 0-100. Live. |
| `MeetBashShieldWrongStaggerPercent` | 85 | Step 82 (decision 78): a bash the raised shield STOPPED with the guard any other way: the BLOCKER's stagger ceiling (S on x in full from `ImpactStaggerFrom` - a block's row; the forced stagger from the tick). Range 0-100. Live. |
| `MeetBashShieldWrongFallPercent` | 40 | Step 82 (decision 78): the same meet: the blocker's fall ceiling (from `ImpactKnockdownFrom`; our own blow from the tick). Range 0-100. Live. |
| `MeetBashShieldWrongShieldDropPercent` | 35 | Step 82 (decision 78): the same meet: the blocker's shield-drop ceiling (from `ImpactShieldDropFrom`; dropped from the tick). Range 0-100. Live. |
| `MeetBashWeaponWrongStaggerPercent` | 95 | Step 82 (decision 78): a bash that LANDED on a weapon guard any other way than right: the knock-back gets through at this ceiling × step 65's brace-failure curve S((x + 0.7) ÷ 1.7) (a spent man: the ceiling); held = braced (no knock-back, no fall, no drop). Range 0-100. Live. |
| `MeetBashWeaponWrongFallPercent` | 50 | Step 82 (decision 78): the same meet: the fall ceiling once the knock-back got through (S on x from `ImpactKnockdownFrom`). Range 0-100. Live. |
| `MeetBashOpenStaggerPercent` | 98 | Step 82 (decision 78): a bash that LANDED on a man with no guard up or mid-swing: the knock-back gets through at this ceiling × the brace-failure curve. Range 0-100. Live. |
| `MeetBashOpenFallPercent` | 60 | Step 82 (decision 78): the same meet: the fall ceiling once it got through. Range 0-100. Live. |
| `MeetBashOpenShieldDropPercent` | 45 | Step 82 (decision 78): the same meet: the shield-drop ceiling once it got through (a man with a shield in hand). Range 0-100. Live. |
| `MeetKickShieldDownStaggerPercent` | 60 | Step 82 (decision 78): a kick met by a raised shield guarding DOWN: the KICKER's stagger ceiling (x_r); the kicked man holds (knock-back refused). Range 0-100. Live. |
| `MeetKickShieldDownFallPercent` | 25 | Step 82 (decision 78): the same correct meet: the KICKER's fall ceiling. Range 0-100. Live. |
| `MeetKickWeaponDownStaggerPercent` | 50 | Step 82 (decision 78): a kick met by a weapon guard DOWN: the KICKER's stagger ceiling; any other guard = step 65's brace and reel as before. Range 0-100. Live. |
| `MeetKickWeaponDownFallPercent` | 30 | Step 82 (decision 78): the same correct meet: the KICKER's fall ceiling. Range 0-100. Live. |
| `MeetAiReadScalePercent` | 100 | Step 82 (decision 78): THE AI READS IT - an AI man targeted by a kick / bash that is starting rolls once at this % × the chance he blocks a swing correctly (`ImpactMeetMath.ReadChance` = the game's `AIBlockOnDecideAbility` × (1 − 3/4 × `AiRandomizedDefendDirectionChance`) - step 30's tired guess inside); read = the correct guard (DefendRight / DefendDown) through step 16's input hook. 0 = never. Range 0-200. Live. |
| `MeetAiReadReactionSeconds` | 0.15 | Step 82 (decision 78): a reader raises the guard this long after the kick / bash began; a hit sooner = a read too late (counted). Range 0-1. Live. |
| `MeetAiReadMaxHoldSeconds` | 1.5 | Step 82 (decision 78): the read guard is let go after this long without the hit (a miss, a kick never played). Range 0.2-3. Live. |
| `ImpactMismatchWeight` | 0.25 | Step 48: x per doubling of the mismatch R beyond the even band. Range 0-2. |
| `ImpactEvenBandDoublings` | 0.5 | Step 48: a mismatch within ± this many doublings (×1.41) = an even match (adds 0). Range 0-3. |
| `ImpactBodyExponent` | 0.5 | Step 48: the body = his CURRENT points to this power. Range 0-2. |
| `ImpactBodyFloorPoints` | 10 | Step 48: the body never counts fewer points (an empty man still has a body). Range 1-200. |
| `ImpactStaggeredBonus` | 0.75 | Step 48: x added on a man reeling from an IMPACT stagger / a lengthened reel (never a fresh man's vanilla flinch). Range 0-3. |
| `ImpactDamageWeight` | 0.0 | Step 48: x per share of his max health the landed blow took - Anton's unfinished point 12 (IMPACT §8 q1); 0 until he answers (the brief), the recommendation was 1.0. Range 0-5. |
| `ImpactRefWeightKg` | 1.5 | Step 48: a weapon / shield of this weight = 1 in the impact and guard numbers. Range 0.1-10. |
| `ImpactWeightExponent` | 0.5 | Step 48: (weight ÷ reference) to this power. Range 0-2. |
| `ImpactRefSwingSpeed` | 90.0 | Step 48: a swing / thrust of this speed (the weapon's modified speed × the tired swing's animation share) = 1. Range 10-200. |
| `ImpactSpeedExponent` | 1.0 | Step 48: (speed ÷ reference) to this power. Range 0-3. |
| `ImpactBluntFactor` | 1.3 | Step 48: a blunt blow's impact factor. Range 0.1-3. |
| `ImpactCutFactor` | 1.0 | Step 48: a cutting blow's impact factor. Range 0.1-3. |
| `ImpactPierceFactor` | 0.8 | Step 48: a piercing blow's impact factor. Range 0.1-3. |
| `ImpactKickFactor` | 1.2 | Step 48: a kick's impact. Range 0-5. |
| `ImpactBashFactor` | 1.0 | Step 48: a bash's impact = the shield's weight factor × this. Range 0-5. |
| `ImpactSkillMinFactor` | 0.75 | Step 48: skill 0's factor K (a straight line to the max at skill 300 - plumbing `ImpactMath.SkillCap`). Range 0.1-2. |
| `ImpactSkillMaxFactor` | 1.25 | Step 48: skill 300's factor K. Range 0.1-3. |
| `ImpactGuardShieldRight` | 1.0 | Step 48: the right-side shield guard × the shield's weight factor. Range 0.05-5. |
| `ImpactGuardParry` | 0.8 | Step 48: the parry guard × the parrying weapon's weight factor. Range 0.05-5. |
| `ImpactGuardShieldWrong` | 0.4 | Step 48: the wrong-side shield guard × the shield's weight factor. Range 0.05-5. |
| `ImpactGuardNone` | 1.0 | Step 48: no guard (a clean hit, a kick) - the body only. Range 0.05-5. |
| `ImpactStaggerFrom` | 0.0 | Step 48: the stagger's S-curve starts at this x (chance = ceiling × S((x − from) ÷ (1 − from))). Range 0-0.95. |
| `ImpactShieldDropFrom` | 0.2 | Step 48: the shield drop's S-curve start. Range 0-0.95. |
| `ImpactKnockdownFrom` | 0.35 | Step 48: the knockdown's S-curve start. Range 0-0.95. |
| `RegenDelayBlowTimes` | 2 | Idle blows before regeneration starts. |
| `BlowTimeSeconds` | 1.5 | How long "one blow" is, for the delay above. |
| `FullRegenSecondsStanding` | 60 | Seconds from empty to full while standing still or walking (the whole refill, whatever the curve below). |
| `RegenRateNearFullPercent` | 50 | Step 14: the refill rate near full, % of the rate near empty - a straight line in the fill in between, empty → full still `FullRegenSecondsStanding`. 100 = the flat refill of steps 5c-13 - defaults.json 100 since step 22 (Anton: "fully linear again, 100% for 60 sec"). |
| `RegenMultiplierAtFullRun` | 0.5 | Regen rate at top speed, relative to standing or walking. |
| `WalkEffortFraction` | 0.4 | Up to this share of top speed counts as walking (full regen). |
| `HideoutBossFightRefill` | true | Step 19 (Anton): in a hideout, the moment the boss fight begins - the duel or the battle - every living fighter on the player's side (the player alone in a duel) refills to full Athletics (up to the health cap), and any attack pause, hold or step back on them ends (§2 "A fresh start"). Off: they face the boss as tired as the first fight left them. |
| `ShowPlayerBar` | true | Player Athletics bar. |
| `ShowPlayerBarOutsideBattles` | true | Outside the fight modes (the training field, towns, villages - the game's walk-about mode) the player bar shows too, while you hold a weapon or a shield or your Athletics is below full; never in a conversation, barter, deployment or cutscene (§3, step 12). Off: fights only. |
| `ShowAttackRecoveryBar` | true | Step 13 (Anton): the Attack recovery bar just above your Athletics bar, shown with it - empties when you attack below the peak line, fills back over your no-attack timer (you cannot attack until it is full), the seconds left inside it ("1.3 s"); full and quiet at full strength. Hidden while `AttackRatePlayerTimer` is off. |
| `FlashBarOnEarlyAttack` | true | Step 13: the Attack recovery bar flashes (two quick pulses) when you press attack while it still fills. |
| `ShowSprintBar` | true | Step 33: your sprint bar, a slim row above the Attack recovery bar, shown with your Athletics bar - "Sprint 6.2 s" and the store as a fill, a white mark at `SprintMinStartPercent`, grey below it, blue ready, yellow while sprinting. Hidden while the sprint is off. |
| `BarYellowBelowPercent` | 75 | Bar turns yellow at or below this % of the peak line (blue just below the line, green above it). |
| `BarOrangeBelowPercent` | 50 | Orange at or below this %. |
| `BarRedBelowPercent` | 25 | Red at or below this % (an empty bar is always red). |
| `ShowFormationSpread` | true | ± spread in the orders-menu strip and under the formation markers: the "± 8" and the lighter band (off: the average alone). |
| `FormationSpreadStdDevs` | 1.0 | Band width in standard deviations (the "± 8" is this width). |
| `ShowReadyCount` | true | Step 24 (Anton): the orders-menu strip (cells and panel rows) and the numbers under the formation markers also show how many men are READY - not bracing (§2 "Brace by orders") - out of the formation, "ready 34/50" (the player not counted, as the strip's other numbers). Hidden while bracing is off (`BraceEnabled`, `AthleticsEnabled`, the master switch). One switch for both places (§3). |
| `ReadyYellowBelowPercent` | 75 | Step 24: the ready count turns yellow at or below this % of the formation ready. |
| `ReadyRedBelowPercent` | 50 | Step 24: the ready count turns red at or below this % ready (the most alarming wins). |
| `ShowInOrderMenu` | true | The orders-menu strip: under each formation card, the men's average Athletics ± spread (bar + "72% ± 8"). |
| `ShowFormationHealth` | true | The orders-menu strip and the formation markers' numbers also show average health ("HP 81%"). |
| `OrderStripUnderCards` | true | true: the strip sits under the vanilla cards (read live) when they can be matched, else the compact panel. false: always the compact panel. |
| `ShowAltMarkerStats` | true | Step 20 (Anton): while vanilla shows its formation markers (ALT held or the orders menu open - the game's own rule, read live), each marked formation also gets "72% ± 8" (the men's average Athletics ± spread, coloured by their average f) and "HP 81%" just under its marker, over a slim bar (§3 item 3). Yours and your allies' always. |
| `AltMarkersShowEnemy` | true | Step 20: the enemy's formation markers get the numbers too (vanilla marks them) - knowing the enemy is tired is the tactical point. Off: yours and your allies' only. |
| `ShowFeverOnLabels` | true | Step 58 (Anton, 2026-10-18: "add those lines as some averages on the group"): each formation's label under its marker also gets its FEVER share, `fever 12/40` (men in a fever of the men counted - the ready count's men), in the fever bar's orange, under `ready 34/50` in the same style; shown only while at least one of its men is in a fever (less noise), every side the labels show. Needs the fever (`FeverEnabled`, Athletics, the master switch). Live. |
| `ShowFeverMarks` | true | Step 44 (Anton): while vanilla shows its formation markers (the same live flag as `ShowAltMarkerStats` - ALT held or the orders menu open), a small bar of ours above each AI man in a FEVER (§2 "Aggression and the fever"; armed or burning): bright orange while armed and at the burn's start, through yellow as the store empties, its fill = the fever left, gone when it is over (§3 item 3b). Calm men and you: nothing. Needs `FeverEnabled`. |
| `FeverMarksShowEnemy` | true | Step 44: the enemy's men in a fever get the mark too (vanilla's own per-man ALT icon is yours and your allies' only - ours is not). Off: yours and your allies' only. |
| `ShowAthleticsMarks` | true | Step 46 (Anton): while vanilla shows its formation markers (the same live flag - ALT held or the orders menu open), a small Athletics bar of ours above EVERY man's head (yours, allies', the enemy's with the next switch; AI men on foot and riders, heroes too; never you - you have your own bar): its fill = his points ÷ his FULL pool, the part his wounds hold dark red (the player bar's way), coloured by the bar's bands (`Bar*BelowPercent`: green at the peak line, blue just below, yellow / orange / red, empty red); just ABOVE his fever bar, at a fixed height whether he is in a fever or not (§3 item 3c). |
| `AthleticsMarksShowEnemy` | true | Step 46: the enemy's men get the Athletics bar too. Off: yours and your allies' only. |
| `AltMarksEveryone` | false | Step 58 (Anton, 2026-10-18: "this ALT view is too cluttered ... only show them on up to 5 soldiers that I'm directly looking at and are really close"; decision 50): false = the NEAR view - the per-man bars (Athletics and fever) only on the men nearest the screen's CENTRE (`AltNearMarksMax`) within `AltNearMarksMetres` of the camera; true = step 46's view, every man on the screen (the nearest `FeverMarkMax`). Both read live; the enemy switches apply in both. |
| `AltNearMarksMax` | 5 | Step 58: the near view's cap - at most this many men get their bars: the candidates (in front of the camera, 1.5 m to `AltNearMarksMetres` away, their side switched on) ranked by the ANGLE between the camera's forward and the line to the bar's point (= nearest the screen's centre, at any resolution, field of view or aspect), projected in that order until the cap is full (a man off the screen takes no slot). `FeverMarkMax` stays the ceiling. Range 1-50. |
| `AltNearMarksMetres` | 15.0 | Step 58: the near view's range - only men at most this far from the CAMERA (to the bar's point) are candidates. The camera, not you: it is where the angle is measured from, it works with you down, in the orders view and under RTS Camera's free camera, and the default third-person camera sits ~3 m behind you (15 m ≈ the men in front of you and the next rank). No distance fade in the near view (vanilla's 1.5 → 8 m icon fade would dim exactly the close opponent you look at). Range 2-100. |
| `FeverMarkMax` | 200 | Step 44: most fever marks drawn in one frame - the men NEAREST the camera (in front of it, at least 1.5 m away) when more are in a fever; the rest wait. Since step 46 the cap counts MEN MARKED - each with his Athletics bar and / or his fever bar (one cap for both, the key kept so no config migration is needed). Since step 58 it is the cap of the "every man" view (`AltMarksEveryone`) and the hard ceiling of the near view (min(`AltNearMarksMax`, this)) - its default and meaning unchanged, so no migration. Range 1-1000. |
| `ShowMoraleMarks` | true | Step 67 (Anton 2026-10-19: "give me eyes on their morale too"): under ALT, a blue morale bar (`Agent.GetMorale()` ÷ 100, read at the formation-stats refresh) at the bottom of each marked man's stack (the near view's 5 men or every man), the Athletics bar's size; the enemy's by `AthleticsMarksShowEnemy`; no bar without morale (tournaments). Live. |
| `ShowMoraleOnLabels` | true | Step 67: "morale 64, fresh 41%" in blue under the near formations' ALT labels - the men's mean morale and mean Athletics (the bar's fill). Live. |
| `MoraleLabelMetres` | 60.0 | Step 67: only formations whose marker distance is at most this get the line. Range 5-500. Live. |
| `MoraleLabelMax` | 4 | Step 67: at most this many - the nearest - get it. Range 1-16. Live. |
| `OrderNoAccidentalCharge` | true | Step 72 (decision 66): in the orders menu a left-button release charges an enemy unit only after a CLICK with the cursor ON its banner icon - within `OrderChargeIconPixels` at the press AND through the hold - and no drag (`OrderChargeDragPixels`); otherwise the game's own unit targeting (`MissionFormationTargetSelectionHandler.SetIsFormationTargetingDisabled`, public) is held OFF from the press through the release frame, so the release finds no unit and the game's troop placer places its move (the dragged frontage kept). Idle = the game's targeting untouched (the F-key targeted charge as vanilla); the gamepad and naval battles: vanilla. Gate: `ModEnabled` FIRST. Off = vanilla (a release while an enemy banner is within 70 px of the screen centre, or an enemy unit within 10 m of the camera, charges it). Advanced, group "Orders: no accidental charges". Live. |
| `OrderChargeDragPixels` | 12 | Step 72: the cursor this far (UI px of a 1080-high screen, scaled by the real height like vanilla's 70) from the press point - the order flag's ground point at the press, followed on the screen as the camera turns - makes the click a DRAG for the rest of the hold: never a charge. Vanilla's own click tolerance is 10. Range 1-200. Live. |
| `OrderChargeIconPixels` | 30 | Step 72: the cursor within this (UI px of a 1080-high screen, scaled) of the game's focused unit's banner icon (its median point 3 m up - where the marker's 50-px icon sits) is ON it. Vanilla's reach is 70 around the screen CENTRE, so more than 70 widens nothing. Range 1-200. Live. |
| `LineDisciplineForPlayer` | true | Step 73 (Toni 2026-10-20, decision 67): the PLAYER's own formations (his team, not left to the AI) keep to the line discipline too - his line orders, the game's own widening and losses are capped like the AI's, and (`LineDisciplinePreviewCap`) the order drag's preview stops growing at the cap. Needs `LineDisciplineEnabled`. Gate: `ModEnabled` FIRST. Off = his lines as wide as he drags them (his clamped widths given back). PLAYER page, "How the AI fights". Live. |
| `LineDisciplineEnabled` | true | Step 73 (decision 67): LINE DISCIPLINE BY TIER - every AI formation in a line, shield wall, loose or scatter arrangement (not cavalry / horse archers; ranged by `LineDisciplineRangedToo`; not squares, circles, columns, wedges; not naval; sieges by `LineDisciplineInSieges`) is at most F = ceil(men ÷ D) files wide (at least the game's own √men), D = the mean of its men's map ranks (`LineDisciplineRanksT0`-`T6`, a hero as `LineDisciplineHeroTier`; the tier = `BasicCharacterObject.GetBattleTier()`, 7 → 6); a line wider than F (the AI's FormOrder Wide / Wider / Custom, an envelop, the game's re-forming, the player's order) or too thin after losses is narrowed to F at once in the logic's pre-tick (`Formation.SetFormOrder(FormOrderCustom(width))`). Gate: `ModEnabled` FIRST. Off = the game's own form orders given back. Advanced, group "Line discipline by tier". Live. |
| `LineDisciplineRanksT0` | 6.0 | Step 73: the minimum ranks a tier-0 man holds (peasants, looters, villagers). Range 1-12. Live. |
| `LineDisciplineRanksT1` | 5.0 | Step 73: a tier-1 man (a recruit) - 120 recruits → D 5 → at most 24 files. Range 1-12. Live. |
| `LineDisciplineRanksT2` | 4.0 | Step 73: a tier-2 man. Range 1-12. Live. |
| `LineDisciplineRanksT3` | 3.0 | Step 73: a tier-3 man. Range 1-12. Live. |
| `LineDisciplineRanksT4` | 2.0 | Step 73: a tier-4 man. Range 1-12. Live. |
| `LineDisciplineRanksT5` | 1.0 | Step 73: a tier-5 man (a veteran) - at 1 a formation of them is never narrowed (F = men). Range 1-12. Live. |
| `LineDisciplineRanksT6` | 1.0 | Step 73: a tier-6 man (an elite) and any higher tier a mod adds. Range 1-12. Live. |
| `LineDisciplineHeroTier` | 6 | Step 73: a hero (`Agent.IsHero` - a lord, a companion, the player) counts as a man of this tier whatever his level. Range 0-6. Live. |
| `LineDisciplineRangedToo` | true | Step 73: archer and skirmisher formations (`FormationQuerySystem.IsRangedFormation`) are held to their depth too; off = infantry and mixed foot formations only. Live. |
| `LineDisciplineInSieges` | false | Step 73: sieges and sally-outs too; off = there the walls, gates and streets set the width (the AI's siege behaviours give custom widths to fit them). Live. |
| `LineDisciplinePreviewCap` | true | Step 73 (decision 67 c): the player's formations carry the cap as their arrangement's `MaximumWidth` (`DisciplinedLineFormation`, the game's plain `LineFormation` with that one override, put in with the game's own `Formation.Rearrange`) - vanilla's order drag preview and its move clamp each formation's width to `formation.MaximumWidth`, so the drag stops growing at the cap. Off = the drag shows any width; the formation is narrowed at the next look after the order. Live. |
| `LineDisciplineShowMessage` | true | Step 73: a line on screen when the player's line order asked wider than his men hold ("your Infantry: these men hold no thinner than 5 deep - 24 men wide at most"), at most once in 2 s per formation. Live. |
| `LineDisciplinePollSeconds` | 1.0 | Step 73: each formation's D is recounted from its men this often, and at once when its count changes; the width is checked every look (every frame). Range 0.25-10. Live. |
| `LineBlockHoldEnabled` | true | Step 74 (Toni's battle 2026-10-20 20:30, decision 68): THE BLOCK HOLD - a formation step 73 holds (D above 1) keeps its BLOCK in a charge (always) and in an advance / move / hold within `LineBlockNearMetres` of the enemy: in its frame (u toward the enemy - the fear band's direction, else its facing; the centre line = the MEDIAN lateral of its men) each man stays within step 73's cap width ÷ 2 + the slack across it and within its depth (ceil(men ÷ files) ranks of `LineBlockRankMetres`, + `LineBlockAheadMetres` ahead, + the slack behind); a man outside is walked back through step 16's input hook (a sixth wish, `Block` - the LAST: a step back's backpedal, the wall's pull, courage's walk back and the fear flock go first), facing the enemy, his place leading the advancing block by `LineBlockLeadMetres`. Gate: `ModEnabled` FIRST, then `LineDisciplineEnabled`, then this; the player's formations by `LineDisciplineForPlayer` (step 73 counts them only then); never the player himself. Off = the game's own charge. Advanced, "Line discipline by tier". Live. |
| `LineBlockSlackMetres` | 2.0 | Step 74: how far past the block's half-width (and its rear) a man may stand before he is walked back, at the full hold. Range 0-20. Live. |
| `LineBlockFullAtDepth` | 3.0 | Step 74: the hold's strength s = (D − 1) ÷ (this − 1) clamped 0-1 - full from this D; D 1 = free (an elite's cap is its whole count). Range 1.5-12. Live. |
| `LineBlockLooseMetres` | 10.0 | Step 74: the extra room (1 − s) × this of a block barely held (D just above 1), 0 at the full hold - across, ahead and behind. Range 0-60. Live. |
| `LineBlockRankMetres` | 1.2 | Step 74: one rank's depth - the block is ceil(men ÷ step 73's cap files) ranks of this. Range 0.5-3. Live. |
| `LineBlockAheadMetres` | 3.0 | Step 74: how far ahead of the block's front (its median + half its depth) a man may run with no enemy within reach. Range 0-20. Live. |
| `LineBlockStrikeReachMetres` | 2.5 | Step 74: a man with an enemy this close (courage's scan) is free to fight ... Range 0-6 (0 = never). Live. |
| `LineBlockStrikeLeashMetres` | 2.0 | Step 74: ... while he stands no more than this beyond the block's allowance (across or ahead); farther he is walked back even fighting. Range 0-10. Live. |
| `LineBlockNearMetres` | 30.0 | Step 74: an advance / move / hold is held once the enemy's front (the fear band's) is this close to the block's front, or a man of it sees an enemy this close; a charge always. Range 0-200. Live. |
| `LineBlockLeadMetres` | 1.5 | Step 74: a walked-back man aims this far ahead of his place while the block moves forward (its speed from its median between frames, faster than 0.3 m/s), so he keeps pace. Range 0-6. Live. |
| `LineBlockMopUpWinPercent` | 50.0 | Step 74: the block lets go when its men's mean win (step 69's - 1 = 2 : 1 local odds, + a shaken enemy) reaches this - they chase the beaten. Range 0-100. Live. |
| `HudRefreshSeconds` | 0.1 | (Advanced) How often the player bar, the orders-menu strip and the formation markers' numbers update (the markers' numbers follow the markers every frame). |
| `PlayerBarWidth` | 205 | (Advanced) Length of your bar in UI pixels of the 1920 × 1080 layout (the game's UI scale applies); 205 = the inside of the vanilla health bar. |
| `PlayerBarHeight` | 12 | (Advanced) Thickness of your bar, UI pixels. |
| `PlayerBarOffsetRight` | 62 | (Advanced) Screen's right edge → your bar's right end, UI pixels (62 = under the vanilla health bar). |
| `PlayerBarOffsetBottom` | 30 | (Advanced) Screen's bottom edge → your bar's row (label, number, bar), UI pixels (30 = the Attack recovery bar fits above it, both just under the health and horse bars; it was 54 until step 13). |
| `RecoveryBarWidth` | 205 | (Advanced) Length of the Attack recovery bar, UI pixels (205 = your Athletics bar's). |
| `RecoveryBarHeight` | 14 | (Advanced) Thickness of the Attack recovery bar, UI pixels (the seconds inside it need about 12). |
| `RecoveryBarOffsetAbove` | 24 | (Advanced) Your Athletics bar's row → the Attack recovery bar's row (bottom to bottom), UI pixels (24 = right on top of it; it moves with your bar). |
| `SprintBarOffsetAbove` | 46 | (Advanced) Step 33: your Athletics bar's row → the sprint bar's row (bottom to bottom), UI pixels (46 = right on top of the recovery bar; as long and thick as the recovery bar - `RecoveryBarWidth`, `RecoveryBarHeight`). |
| `OrderStripTextSize` | 13 | (Advanced) Font size of the strip's numbers, UI pixels. |
| `OrderStripTextOffset` | 1 | (Advanced) A card's bottom edge → the top of its numbers, UI pixels (they sit left and right of the vanilla order icons). |
| `OrderStripBarOffset` | 20 | (Advanced) A card's bottom edge → the top of its strip bar, UI pixels (20 = just under the vanilla order icons). |
| `OrderStripBarHeight` | 4 | (Advanced) Thickness of the strip bar, UI pixels. |
| `OrderStripReadyOffset` | 24 | (Advanced) Step 24: a card's bottom edge → the top of its ready count, UI pixels (24 = right under the strip bar; the cell is then 40 px deep - the gap between two stacked cards). |
| `OrderStripSideMargin` | 2 | (Advanced) How far the numbers and the bar keep in from a card's sides, UI pixels. |
| `OrderPanelOffsetTop` | 80 | (Advanced) Screen's top edge → the fallback panel (centred), UI pixels. |
| `OrderPanelWidth` | 300 | (Advanced) Width of the fallback panel, UI pixels. |
| `AltMarkerTextSize` | 16 | (Advanced) Font size of the numbers under the formation markers, UI pixels (vanilla's marker count is 22; same font and outline - the brush NameMarker.Distance.Text). |
| `AltMarkerOffset` | 2 | (Advanced) A formation marker's bottom edge (its distance row) → the top of our numbers, UI pixels (negative = up over the marker). |
| `AltMarkerBarWidth` | 60 | (Advanced) Length of the slim bar under the marker's numbers, UI pixels (60 = the marker's distance row). |
| `AltMarkerBarHeight` | 3 | (Advanced) Thickness of that bar, UI pixels; 0 = no bar. |
| `FeverMarkWidth` | 22 | (Advanced) Step 44: length of a fever mark near the camera, UI pixels (the game's UI scale applies; full size to 20 m, a line down to 60% at 60 m). Range 4-100. |
| `FeverMarkHeight` | 5 | (Advanced) Step 44: thickness of a fever mark, UI pixels (smaller far away like the length). Range 2-30. |
| `FeverMarkHeightMetres` | 2.5 | (Advanced) Step 44: the mark's bottom edge, metres above the man's feet (vanilla's ALT icon over your men sits at 2 m). Range 1-4. Since step 46 the bottom of a man's whole stack (the fever bar, the Athletics bar above it); a rider's stack 1 m higher (plumbing `RiderLiftMetres`). |
| `AthleticsMarkWidth` | 22 | (Advanced) Step 46: length of the Athletics bar above a man near the camera, UI pixels (smaller far away like the fever mark). Range 4-100. |
| `AthleticsMarkHeight` | 5 | (Advanced) Step 46: thickness of that bar, UI pixels. Range 2-30. |
| `AthleticsMarkGap` | 2 | (Advanced) Step 46: the fever bar's top → the Athletics bar's bottom, UI pixels; the fever bar's place is kept while he has none (only while fever bars can show for his side at all). Range 0-20. |
| `FormationStatsRefreshSeconds` | 0.25 | (Advanced) How often formation averages and spreads are recomputed - every formation of every side since step 20. |
| `ImpactSpike` | false | (Advanced) Step 47, IMPACT I1 (docs/IMPACT.md §7): a TEST TOOL, not gameplay - while on, fixed loud values prove the six engine levers of the coming impact system in a real battle (a kick's KnockDown, the block stun x3 with the cap at 2 s, a reaction at speed 0.5, a forced stagger, a dropped shield, a knockdown blow of our own) and the `[summary] impact spike` lines give each a verdict WORKS / FAILS / UNSEEN with what it means for steps 48-53 (PLAYTEST D16). Its values are documented constants (Core `SpikeMath`), not settings. Master switch first; live (off = the game's stun cap back at once, nothing queued is played). |
| `VerboseLogging` | false | Log every roll, blow and exhaustion (rate-limited) to `trax_combat.log`. Off = load, settings, mission start/end, per-battle summaries and errors only. |
| `LogMaxMegabytes` | 8 | (Advanced) Size limit of `trax_combat.log`, MB. Past it a trim cuts the oldest VERBOSE lines, down to about half; every other line is kept (§4 "The log"). |

New parameters discovered while building go into this table in the same commit.

## Planned parameters (§3 additions — not in the schema yet)

The step that builds each one moves its row into the Parameters table above (the schema
test reads that table only) and removes any retired rows in the same commit. (Step 5d moved
its four `StepBack*` rows up and added `StepBackEnemyRange`, `StepBackHoldAttacks`,
`StepBackMaxAtOnce`; step 6 moved the three `Bar*BelowPercent` rows up and added the four
`PlayerBar*` layout rows; step 9 moved `ShowFormationHealth` up and added `OrderStripUnderCards`,
the five `OrderStrip*` and the two `OrderPanel*` rows; step 20 built step 8's `ShowFormationBars` as
`ShowAltMarkerStats` - the "only while vanilla shows formation markers" form - and added
`AltMarkersShowEnemy` and the four `AltMarker*` layout rows.)

The two features Anton moved to LATER (2026-09-27, §3 items 2 and 3) kept their rows here. They
were in the schema, MCM and the config file until step 10b took them out (review R21: no switch
that does nothing). The step that builds a feature moves its rows back up (and into the schema,
TraxSettings and defaults.json). The designs are §3 and AI_NOTES "Step 7" / "Step 8" / "Step 20".
Item 3 is built (step 20, hold ALT); what is left of it is an always-on form above the formations.

| Key | Default (initial) | What it does | When |
|---|---|---|---|
| `ShowTargetBar` | true | Bar for the fighter you look at. | LATER (step 7) |
| `TargetBarMaxDistance` | 30 | Metres — how far away a looked-at fighter still gets a bar. | LATER (step 7) |
| `TargetBarLingerSeconds` | 2 | Seconds the bar stays after your aim leaves the fighter. | LATER (step 7) |
| `FormationBarsAlways` | false | true: the formation numbers always shown above the formations (our own projection - step 20's fallback), not only while vanilla shows its markers. | LATER (an always-on form of step 20) |
| `FormationBarHeight` | 3.0 | Metres above the formation's centre for those always-on numbers (vanilla's markers use 3). | LATER (an always-on form of step 20) |

`ShowFormationSpread`, `FormationSpreadStdDevs` and `ShowFormationHealth` stay in the table
above: the orders-menu strip and the numbers under the formation markers (step 20) use them.

Retired in step 5c (Athletics v2): `MaxAthletics` (→ the pool is the Athletics skill),
`FullRegenSecondsMoving` and `MovingSpeedThreshold` (→ regen by effort),
`ExhaustedRecoverPercent` (→ the gradual curve has no recovery line).

## Interpretations (Anton can overturn any of these)

1. **"A blow" = an attack the fighter makes** — swing, thrust, shot, throw — landed or not.
   Anton's damage rule says "each time it lands"; the Athletics rule does not, so every
   attack costs. `CostOnMiss = false` switches to landed-only.
2. **Hero multiplier is 0.75.** The ask said 0.75 and also "0.5, 10 → 5"; the leader
   formula 0.75 × 0.75 × 10 settles it at 0.75. One number to change if 0.5 was meant.
3. **"Party leader" = the hero leading the fighter's own party** — the player, and AI lords
   for their own parties. Stacks with the hero multiplier.
4. **Blocking, running and riding cost nothing.** Only blows drain. (Blocking: confirmed by
   Anton, 2026-09-27.)
5. **Weakening is gradual below the peak line** (Anton's pick for Athletics v2, step 5c) — it
   replaced the first ask's cliff at 0. "Exhausted" still names E = 0.
6. **Applies in every combat mission** (field battles, sieges, hideouts, custom battles,
   tournaments and arena). Per-mission toggles only if playtest asks for them.
7. **Custom battle has no parties**: there the "party leader" is the side's general, or —
   when a side has none — every hero on that side.
8. **Kicks and shield bashes cost `CostPerKickOrBash`** (3 - Anton, 2026-09-28, step 18; free until
   then - see 19); a couched-lance / braced-spear hit costs one blow when it lands; siege engines are free.
9. **Shield damage is not randomized** by default; horse-charge bumps follow the melee
   toggle; fall damage and hits on objects never roll.
10. **RBM is declared incompatible** (Anton confirmed, 2026-09-27) — see §5.
11. **Step backs happen in field battles only** (step 5d, Claude's call): not in tournaments,
    arena fights, duels or naval battles (nobody there to step in; moving decks), not from a
    shield wall, square or circle, and the roll comes when the swing ENDS ("after each swing"),
    so the swing always completes first. Two numbers the spec did not have became settings:
    `StepBackEnemyRange` (only with the enemy close) and `StepBackMaxAtOnce` (a cap, so a whole
    front line never steps back together); `StepBackHoldAttacks` makes "guard up" a switch.
12. **The player bar's details** (step 6, Claude's calls): it sits UNDER the health bar (RBM's
    place for its bars too), with a label *Athletics* so a new player knows what it is; the
    colour thresholds apply in order of alarm (if they are set out of order, the most alarming
    band that applies wins; an empty bar is always red); the colours themselves are fixed in the
    code (MCM has no colour picker) - `BarMath` in Core, one place to change; empty = red text
    and frame rather than a pulse (a pulse at a 0.1 s refresh would stutter); stealth missions
    count as fights. The bar's size and place became four Advanced settings (`PlayerBar*`).

13. **The attack rate** (step 5e, Claude's calls): the AI's pause follows m through the AI's own
    attack values AND an exact hold after each melee swing - two techniques because the native
    meaning of the AI values cannot be seen, each an A/B switch so the playtest can keep the one
    that reads on target; the hold is melee-only and never for the player or riders; the AI's
    "hold a readied blow" time is NOT lengthened (a raised weapon means a lowered guard - tired
    men must not defend worse); cycles with a step back in them are left out of the measurement
    (that pause is the step back's). Step 13 (item 16) replaced the hold's target and scope: the
    hold is now the no-attack timer after every attack, melee and ranged, riders too.

14. **The orders-menu strip** (step 9, Claude's calls): the numbers are SHARES of each man's own
    bar ("72%"), not points (pools differ per man since 5c); the "± 8" is the band's half-width
    (`FormationSpreadStdDevs` × σ, so number and band agree) and `ShowFormationSpread` switches both
    off; health reads "HP 81%" (the game's font has no ♥); the squad numbers leave the player out
    (the cards count the men under his command — his own bar shows him), which also changes the
    summary's "your formations" line; the numbers sit left and right of vanilla's order icons and
    the bar under them; the fallback panel sits at the top centre; `OrderStripUnderCards` lets
    Anton force the panel if the strip ever looks wrong.

15. **The log's trim and the LATER settings** (step 10b; review R7 - the manager's decision, R21):
    a trim never cuts a non-verbose line (the `~` mark tells them apart), so turning
    `VerboseLogging` on costs nothing the playtest reads, and the cap became `LogMaxMegabytes` (8);
    the six settings of the two LATER features left the schema (no switch that does nothing) and
    wait in "Planned parameters"; the Defaults buttons moved above Advanced.

16. **PAUSE ONLY** (step 13, Claude's calls on Anton's playtest call - AI_NOTES "Step 13"): D is the
    wind-up (not a blow held ready) + the release, and for ranged + the reload after the loose (the
    pause starts when it ends); m is taken when the attack ends; pauses under 0.1 s are not
    started; your hold begins at your release's start when its pause is sure to be ≥ 0.1 s (so a
    click during your swing cannot chain a blow past it) and is never applied during a ready;
    kicks are never held, shield bashes (the attack button while blocking) wait like every attack;
    the reload is never held; the AI's timer covers riders too (without the animation slow-down a
    tired rider would attack at the full rate); `AttackRateAiDecisions` is off by default (it
    double-counts on top of the timer) and config format 2 turns an old file's `true` off once;
    the verdict's target stays the fresh cycle ÷ m (the rate × m), the timer is checked on its own
    (the gap it leaves, attacks inside it); your timer has its own switch
    (`AttackRatePlayerTimer`) as an escape hatch; the recovery bar hides with your timer off, the
    Athletics bar's row moved to 30 px to make room (format 2 moves an old 54).

17. **The run-speed floor and the refill curve** (step 14, Claude's calls on Anton's two asks - AI_NOTES
    "Step 14"): the floor 0.7 is a design change, so DESIGN's initial value moved with it, and config
    format 3 moves a config.json still holding the old default 0.3 to 0.7 once, logged (a hand-set 0.3
    cannot be told apart and moves too - the log line names it); "half linear" = the rate near full is
    half the rate near empty (`RegenRateNearFullPercent` 50), a straight line in the fill between; the
    fill is measured on the FULL pool (wounds do not change the rate at a given bar level);
    `FullRegenSecondsStanding` keeps its meaning (empty to full), so the curve moves time from the top
    of the bar to the bottom and never makes refilling longer overall; each regen step is integrated
    exactly (the step length never changes the result, 100 is the old rule to the bit); the slider runs
    10-100 (0 would never reach full, above 100 would refill slower when low).

18. **The guard really up and the backpedal** (step 16, Claude's calls on BATTLE_PACING lever #2 with Anton
    asleep - AI_NOTES "Step 16"): one per-man input component does both the AI's pause and the step back,
    added the first time a man is held (never at spawn, never for fresh men), so it costs nothing for men
    who are never held; the engine's input callback is turned off again when he is idle - unless another
    mod's component wants it (RTS Camera Command System turns it on for every agent; ours is added after
    its, so ours writes last, and the two never move the same man: RTS's defensive hold acts only in shield
    wall / square / circle, where nobody steps back). Only the attack bits are taken out; a held man who
    wants to attack raises a guard (`DefendDown` - RTS Camera's own cancel; a switch, `AiHoldRaiseGuard`);
    in a ready the guard is always pressed, because attack bits vanishing mid-ready would RELEASE the
    blow. The backpedal is full stick (the engine's backpedal speed and the tired run cap decide how fast
    - plumbing, not a number to tune) along the fixed line the safety probe cleared for the whole
    distance, re-checked 0.6 m further back every 0.25 s. The pause survives a step back both ways round
    (the later end frees the attacks); the old NoAttack technique cannot sit under our own scripted frame,
    so its hold waits and is set the tick the walk ends. Both new ways are A/B switches (`AttackRatePaceByInput`,
    `StepBackBackpedal`, on); a hold or a step already running finishes the way it began. Cycles with a step
    back inside now COUNT in the attack-rate verdict (the pause survives them) and are shown apart. The
    pause itself is still DESIGN's D × (1/m − 1): the AI's own gap after an attack runs inside it, so the
    verdict against the fresh cycle ÷ m may read "too fast" where NoAttack's 1-3 s re-decision used to
    fill the gap - the timer rows and the new "floor D/m" say whether the SPEC holds.

19. **Kicks and shield bashes cost Athletics** (step 18, Claude's calls on Anton's "3, a slider, hero/leader
    multipliers apply like any blow" - AI_NOTES "Step 18"): charged when the kick or bash STARTS, landed or
    not - `CostOnMiss` stays a rule for blows (a kick is one short move, and a landed-only kick would be
    free exactly when it fails); a cost of 0 is free in every way (not even the refill delay restarts), a
    cost above 0 restarts the refill delay like a blow (it is effort); it is never a blow - `Blows`, the
    "blows at full strength" counts and the attack-rate numbers leave it out, it starts no pause and no step
    back. Where a kick is seen is not proven in game (a shield bash showed on the upper-body action channel
    in the log of 2026-09-27; the game's own code reads kicks on the whole-body channel), so both channels
    are read (the whole-body one on foot only - riders never kick) and a kick on both is one kick; a kick
    or bash that LANDS while neither channel shows one is charged at its hit, and the summary says how many
    each way. A mounted bash is charged like any bash if the engine ever plays one (vanilla has none).

20. **The hideout boss fight's fresh start** (step 19, Claude's calls on Anton's ask - AI_NOTES "Step 19"): the
    moment is the game's boss objective appearing (not the cutscene, not the talk - the fight itself); "the
    player's side" is the game's own teams at that moment (so the duel's onlookers are left out by the game's own
    rule); the refill goes to the top a man can refill to (his wounds cap it), not past it; it also ends every
    running pause, queued pause and step back of those men (a fresh man has none), and the measured cycles do
    not span it; the boss's side is only measured, never touched; one fresh start per mission.
21. **The numbers under the formation markers** (step 20, Claude's calls on Anton's "hold ALT" - AI_NOTES
    "Step 20"): they show exactly while vanilla shows its markers - the game's own flag read live, which
    is ALT held OR the orders menu open (so they show with the orders menu too, beside the strip); under
    the marker, not over it (the marker's column reads count, icon, distance, then ours - and nothing of
    vanilla's is covered); allies' formations count with yours (always shown), only the enemy's have a
    switch; the player is left out of his own formation's average (as in the strip), everyone else on
    every side is counted; the numbers are coloured by the men's average f rather than drawing a second
    colour key; they show with the player down too, because vanilla's markers do.
22. **Anton's tuned defaults** (step 20b, Claude's calls on Anton's playtest words of 2026-09-28 - AI_NOTES
    "Step 20b"): 0.6 and 85 are TUNINGS, so they live in defaults.json and this table's Default column keeps
    the initial 0.7 and 100; the swing animation's RULE changed (a straight line by f, not max(m, A)), so the
    text above changed with it; f is read off the attack multiplier the decorator applies (the line moves in
    m's recompute steps, and attacks never slowed = animations never slowed); D is built at full animation
    speed, so the pause in seconds is exactly step 13's and the slower swing adds only its own time - the
    verdict's target adds that time too, so a tired band does not read "too slow" because of it; the slider
    stops at 5, not 0 (the line reaches 0 at empty - a frozen swing); config format 4 moves a format-3
    file's 0.7 and a format 2-3 file's 100 once, logged (a value he set himself stays - his config.json of
    2026-09-28 held his own playtest values 0.5 and 90, so it keeps them).

23. **Battle pace** (step 21, the manager's decisions on Anton's two asks + Claude's calls - AI_NOTES "Step 21"): the
    class is read at each attack's RELEASE from what he holds (a shield in the off hand; the main hand's weapon class
    Bow / Crossbow), not his formation; "infantry" = on foot, so a shield on horseback is a rider (no share); it
    stacks on tiredness multiplicatively on the EXPECTED cycle, not on D alone (the data check: the AI's own ~1 s gap
    after an attack would have hidden half of a D-based share), with the AI's gap a setting (`AiMeleeGapSeconds`,
    measured 1.0 s) the summary checks; the archers' extra is flat seconds on top (the brief's own form), sized from
    the log's fresh cycles - the log does not split bows from crossbows, so bows 2.0 s (bow-heavy battles read
    4.3-4.5 s) and crossbows 2.5 s (their reload is ~1.5 s longer); thrown weapons and slings get no share (not
    archers; javelin men throw a few and close in); AI heroes are included; the player never; it rides on
    `AttackRatePaceHold`, so that switch off means no AI pause at all; fresh men are now held too, so nearly every
    foot soldier gets the step-16 input component at his first attack (one small object per man, once - the AI
    timer's tick over 1000 held men cost ~0.005 ms and allocated nothing in the smoke); the "swing less" sliders stop
    at 90 (100 = ÷ 0).

24. **Defending costs, the refill straight again, the run floor 0.3** (step 22, Claude's calls on Anton's three asks of
    2026-09-29 - AI_NOTES "Step 22"): the DEFENDER pays - the man whose shield or weapon stopped the blow (the game's
    melee collision: `AttackBlockedWithShield` with `CorrectSideShieldBlock` for the side; else a Blocked / Parried /
    ChamberBlocked result = a weapon parry); a paid block restarts the refill delay, exactly as a kick does (step 18) -
    so a man kept busy blocking does not refill, which is the point of "slower battles"; a cost of 0 is nothing at
    all (no delay restart); ONE charge per blocked blow - the same attacker's same swing (his release counter; a
    couched lance or an untracked attacker: by time) within 1 s is the same blow, a new swing or another attacker a
    new one; missiles stopped by a shield are free for now (counted), and so are a blocked kick or bash (not blows)
    and a blow the shield on the back stopped (no defence was made); riders pay like men on foot; AI heroes and the
    player pay with their multipliers; the step back, the attack pause and the damage roll never look at a block.
    100 and 0.3 are TUNINGS (defaults.json; this table keeps the initial 50 and 0.7, as step 20b did); config format 5
    moves a format 3-4 file's refill 50 and a format-4 file's run floor 0.6 once, logged - both were exactly what
    Anton's own config.json held (format 4, 0.6 and 50), so his next game runs 100 / 0.3 without a hand edit.

25. **Brace by orders** (step 23, Claude's calls on Anton's ask of 2026-09-29 and the manager's brief - AI_NOTES "Step
    23"): "the bar" = points ÷ the FULL pool (what the fill shows), not f; the order is the formation's movement order
    read at every look, and an AI-commanded formation on a plain Move reads as ADVANCE while its behaviour is
    BehaviorAdvance / CautiousAdvance / Vanguard (the team AI advances on Move orders - read literally, every AI
    advance would sit on the hold floor 60) and as CHARGE under BehaviorTacticalCharge; attacking a gate or engine is a
    charge; the wound cap keeps the band's width under the cap (floor = min(order floor, cap − margin)), so a wounded
    man is never stuck and never braces full to his cap; the per-man margin is rolled once per man and battle at his
    first look and the spread slider rescales every roll live (his place in the spread is kept); the brace uses ONLY
    step 16's input technique (no NoAttack fallback - its own wish, so no hold's end can lift another); ranged is let
    through by what he holds (read about every 0.25 s) and by a ranged action under way; the shield: taken out with
    TryToWieldWeaponInSlot (a one-hander first after a two-hander), at most 3 calls a brace, nothing forced back at its
    end; `EnforceShieldUsage` was NOT used (vanilla's formation update rewrites it for every AI agent; RBM needs a Harmony
    patch to keep it) - the shield is kept up through the input instead (`BraceRaiseShield`); riders and AI heroes
    brace, the player never; the formation read API counts Bracing / Ready for the next step's "ready men".

26. **The ready count** (step 24, Claude's calls on Anton's ask of 2026-09-29 - AI_NOTES "Step 24"): "ready" = not
    bracing, step 23's `Ready` of `Total` - the same men the strip's other numbers count, so the player is left out of
    his own formation (under an ALT marker of his own formation it may read one fewer than vanilla's count above); the
    words "ready 34/50" (the card and the marker already show the formation's size, but "34/50" alone would not say
    what it counts); plain text (the brushes' own #E8E8E8) while most are ready, yellow at or below 75%, red at or below
    50% (the bars' own yellow and red; the most alarming wins); HIDDEN while bracing cannot happen (the master switch,
    `AthleticsEnabled`, `BraceEnabled`) rather than a constant "all ready" - less noise; ONE switch for both places
    (`ShowReadyCount`, beside `ShowFormationHealth`, which also serves both). On the strip it is a third row under the
    bar (a 40 px cell = the gap between two stacked cards), not squeezed beside the numbers (the 35 px beside the
    order icons hold "72% ± 8" already); at 1920 × 1080 the bottom card of a column then has 24 px to the screen's edge,
    so its cell is lifted 16 px (only while the row is drawn - with bracing off it is step 9's cell again). Under the ALT
    markers it is a third line under the bar, centred.

27. **Overall damage reduction** (step 25, Claude's calls on Anton's ask of 2026-10-10 - AI_NOTES "Step 25"): "all
    sources" = every hit our damage decorator sees on a person or a horse, shield blocks included - melee, missiles,
    kicks / bashes, horse charges, friendly fire, tournaments too (a blunt tournament hit is still a hit); NOT falls
    (not a blow - a jump from a wall should not grow safer) and NOT objects (gates, rams, siege engines, ships: a siege
    should not drag on for a men's-fight setting). AFTER the roll, so the roll's ±50% spread stays a share of the
    reduced hit and the average is exactly × 0.75. Independent of the roll's switches (the roll is "random", the
    reduction is "weaker" - one off must not turn off the other); the master switch alone makes it vanilla. Shields
    are reduced with no toggle of their own (all sources; shields lasting longer is the same "slower" Anton asked
    for) - one line to change if he wants them at the game's pace. Not on the stun / knockback numbers the game
    computes elsewhere: only the damage.
28. **Only a wrong-side block stops the refill** (step 29, Claude's calls on Anton's rule of 2026-10-12 - AI_NOTES
    "Step 29"): the delay clock is the fighter's last-blow time; a block that does not restart the delay leaves that
    clock AND a running refill run untouched (only the points move), so the refill never stalls under a stream of
    clean blocks - it dips by each cost and climbs on. Whether he nets up depends on the rates by design: at the
    shipped flat refill (empty to full in 60 s at a walk) a pool of P points refills P/40 points per 1.5 s, against
    a parry's 2 × his multiplier - a soldier needs a pool above 80 to climb while parrying every 1.5 s, a hero above
    60, a hero who leads his party always climbs (floor 50); a right-side shield block (1 ×) always nets up. The
    player and the AI alike; one switch, live; off = step 22 to the bit.
29. **Tired AI blocks in a random direction** (step 30, Claude's calls on Anton's rule of 2026-10-12 - AI_NOTES
    "Step 30", RESEARCH §M). This deliberately REVERSES step 5e's principle "tired men must not block less" (the
    guard-by-f check of steps 5e / 16 was there to keep the guard): for the AI, exhaustion now costs guard quality
    too, so a duel between two exhausted men ends in a fight, not a 95% wall. The lever is the game's own
    per-agent random-direction chance (raised, never lowered), not the AI input bits: the native AI rolls it per
    defence decision, nothing of ours runs per frame, riders are covered, and the cost is one float per
    recompute. If PLAYTEST D8 shows the engine ignoring it, the fallback is replacing his direction bits in
    `OnAIInputSet` (step 16's hook) once per incoming attack - designed in AI_NOTES "Step 30", not built.
30. **The swing stack** (step 32, Claude's calls on Anton's design of 2026-10-12 - AI_NOTES "Step 32"): (a) the spend
    is at the RELEASE (a cancelled ready or a feint costs nothing; the player's gate keeps a ready from starting at 0);
    (b) "not attacking" = not in a ready, a release or the reload after a loose - a block recoil and the AI's guard count
    as rest; (c) the refill's D is his LAST measured attack's (0.8 s before his first), its m his current one, so a
    resting man's refill speeds up as his bar climbs; (d) an attack at an empty stack (a chained blow, a gate miss)
    loses the refill made so far - an attack is never free; (e) the AI's own gap after a melee swing from the stack
    does not refill (above) - Anton's "the average rate is unchanged" holds for the AI only with it; (f) the player's
    kick is gated only on foot (a mounted "kick" flag is the game's own); (g) the AI's kick is taken out only by the
    input technique (`AttackRatePaceByInput`, on by default); (h) a kick that spends an AI man's last swing starts
    his wait like an attack's end would. Each is a default Anton can overturn.
31. **The sprint** (step 33, Claude's calls on Anton's design of 2026-10-12 - AI_NOTES "Step 33"): (a) it needs
    Athletics on (the store is made of its points); (b) "moving" for the drain = above 0.5 m/s, for the refill = above
    a walk (effort > `WalkEffortFraction`), as the Athletics refill reads it; (c) only a key PRESS starts your sprint -
    holding the key through a dry store never restarts it by itself (no accidental sprints); (d) a menu or the order
    menu taking the keys ends it, a horse refuses it; (e) the AI's "new order" = the order KIND changes (charge ↔
    advance ↔ hold …), polled every 0.25 s per formation - the same kind given again (a second charge) is no new
    order; the first look at a formation and an order given while the AI sprint was off are not new; (f) a new order
    ends a running AI sprint and the new share is of what is left; (g) "in melee" = an enemy within 3 m, looked at
    every 0.25 s; (h) the key is a NAME from a fixed list (an MCM text box; an unknown name is refused, the old key
    kept) - not every game key, so a fight key cannot be picked by mistake; (i) the multiplier moves in 0.05 steps
    (about 12 recomputes a man over a full sprint) and the recompute budget's start rotates, so a 1000-man charge is
    queued over ~20 ticks without starving anyone. Each is a default Anton can overturn.
32. **Sprint tuning** (step 35, Anton 2026-10-12 after two tournaments - AI_NOTES "Step 35"): (a) **the peak is on the
    FRESH run** (Anton's call): `SprintMaxMultiplier` × an untired run, whatever the bar - a tired man's dodge is as quick
    as a fresh one's at its start, and the fade to `SprintMinMultiplier` × his CURRENT run is where the tiredness shows;
    (b) the store's floor `SprintMinSeconds` (2 s) refills by the same rule as the store (the whole store in
    `SprintRefillSeconds` ÷ the factors), so it never refills faster than a full store would; (c) with a floor there is no
    "empty bar" refusal - a used-up store is "below the 20%"; (d) the acceleration cap is written only while sprinting (the
    decorator runs the base model first and leaves the game's value otherwise; the start, every 0.05 step, the end and a
    sprint setting change each recompute him); (e) no config migration: Anton's own config.json already holds 2.0 (set in
    MCM), a fresh install takes defaults.json's. Each is a default Anton can overturn.
33. **The AI sprint by order** (step 36, Claude's calls on Anton's rule of 2026-10-12 - AI_NOTES "Step 36"; step 37 kept
    the keep alone - see (f)): (a) the use and the keep are shares of his FULL store (its capacity now: current points ÷ `SprintPointsPerSecond`, never below
    `SprintMinSeconds`), not of what is stored, so a move from full and a move from 80% differ only by the reserve; (b) the
    AI has no `SprintMinStartPercent` gate any more - the keep is its gate, and a sprint shorter than 0.2 s (plumbing,
    `SprintMath.AiMinSeconds`) is not started (a charge from 10% still sprints its 0.5 s); the player's 20% gate is
    unchanged; (c) "move" = the game's Move order that is neither the AI's advance (`BehaviorAdvance` /
    `CautiousAdvance` / `Vanguard`) nor its tactical charge - F1 F1, and an AI general's Move under defend / skirmish /
    flank and the like; follow and follow-an-entity stay "none" (a man escorting someone should not dash off), as do
    hold, retreat and fall back; (d) the summary counts "reserve kept" apart from "less than 0.2 s or empty"; (e) old
    keys: config format 6 - `SprintAiChargeSharePercent` becomes `SprintAiChargeUsePercent` (its value kept unless the new
    key is set), `SprintAiMoveSharePercent` is dropped (it was advance and move together, as a share of the stored
    seconds - the new four take their defaults); both leave the file at its rewrite instead of sitting under "Not
    recognised"; (f) step 37 (Anton, same day: "simpler - one number per order, the reserve"): the three uses are gone, he
    sprints everything above the keep (stored − keep x capacity); keeps 0 / 50 / 80; an order with keep 100 never sprints.
    Config format 7 drops the three `SprintAi*UsePercent` keys (and step 33's two shares, renamed no more) with one
    [config] line each, and moves a format-6 file's `SprintAiAdvanceKeepPercent` 0 → 50 and `SprintAiMoveKeepPercent`
    70 → 80 only when they still hold step 36's defaults. Each is a default Anton can overturn.
34. **The shield wall holds its slots** (step 41, Claude's calls on Anton's report of 2026-10-17 - AI_NOTES "Step 41"):
    (a) **on by default** - it is the fix for what he reported, it changes nothing outside the three defensive
    arrangements, and off is one switch, live; (b) all three defensive arrangements (shield wall, square, circle), not the
    shield wall alone - square and circle are the same "stand and hold" orders and bulge the same way; (c) any order but
    charge / retreat (a halt and an advance hold too - the brief and RBM differ here: RBM holds Moves only; under a halt
    vanilla never writes the flag, so ours simply stays); (d) the slot is vanilla's own point (`blendWithOrderDirection`
    true - the very value vanilla hands the engine), so only the flag differs from vanilla; (e) a scattered formation
    (vanilla's "deviation > 3 × speed": it writes zeros and lets everyone go) is held too - forming the wall is the point
    of the order (RBM does the same); (f) a siege's DEFENDERS are held (a wall line on the battlements is exactly a held
    line; RBM excludes only attackers), naval battles not at all (ship decks and boarding are unknown ground - the step
    back skips naval for the same reason); (g) no reflection on the private `ShouldCatchUpWithFormation` (it only paces
    speed; vanilla keeps it); one read-only reflection: vanilla's per-man update TIMER, to write right after it (RESEARCH
    §O) - if a game update renames it, a rewrite every 0.1 s takes over and the summary says so. Each is a default Anton can
    overturn.
35. **A small step out of the wall - the leash** (step 42, Claude's calls on Anton's playtest of 2026-10-17 - AI_NOTES "Step
    42"): (a) L = 1.0 m to start ("just a bit of a step"), 0-3, 0 = step 41's hard hold byte for byte; (b) the flag ON / OFF by a
    two-state leash, not a slot point moved toward the target (the engine's use of that point is invisible - RESEARCH §O item 8);
    (c) "close in front" = within L + 2 m of his SLOT, in front of its line (a strike's reach after a step of at most L - an enemy
    farther off would only draw him out and back without a blow); (d) after a step he may step again only once back within
    min(0.3 m, L/2) of his slot, and a man out keeps 0.5 m of grace on his enemy's distance - no jitter at either edge; (e) a
    square's or circle's man steps OUTWARDS (from the formation's centre through his slot), not along the formation's facing;
    (f) a man out is looked at every 0.1 s, held men keep step 41's cadence. Each is a default Anton can overturn.
36. **Aggression and the fever** (step 43, Claude's calls on Anton's design of 2026-10-17 - AI_NOTES "Step 43"): (a) melee only -
    the two attack-decision chances; archers keep their own wait (step 21); (b) AI on foot only - riders keep the game's
    values (a less eager rider misses his pass), never the player; (c) no floor of ours under the game's 0.05 clamp; (d) the bar
    = points ÷ his full pool, a wound's cap in it, no special case; (e) the share in 0.05 steps (rounded to the nearest), riding
    the speeds' re-target and its recompute budget; (f) with step 5e's switch on the two factors multiply; (g) the same order
    kind again does not re-arm, the other aggressive kind re-arms to the larger of what is left and a fresh fever; (h) a hold /
    move / retreat ends a BURNING fever too; (i) the fever sprint is a roll every 1 s at 50% × the fever left (Anton's change of
    the once-per-fever rule), the reserve still capping it; (j) heroes the same; (k) a man who mounts loses his fever; (l) the
    shield is read live at each ~0.25 s look. Each is a default Anton can overturn.
37. **The fever mark under ALT** (step 44, Claude's calls on Anton's ask of 2026-10-17 - AI_NOTES "Step 44"): (a) our own 2D mark,
    never a tint of vanilla's per-man icon (private mesh list, rewritten on every highlight - the two would fight); (b) WHEN = step
    20's flag (vanilla's formation markers shown: ALT or the orders menu), not the per-man icon's own rule (ALT, the "always show
    troop banners" option, or only the SELECTED formations while the orders menu is open) - one rule for both ALT overlays, and the
    orders menu is exactly when you choose to charge; (c) a bar whose fill is the fever left, orange → yellow, not a dot; (d) the
    enemy's on by default (vanilla's icon never marks the enemy; seeing their surge is the tactical point); (e) the cap picks the
    nearest ON THE SCREEN (each eligible man is projected first); (f) vanilla's icon fade (1.5 → 8 m), 60% size at 60 m; (g) the
    layout numbers are Advanced settings (step 20's precedent), the colours UI constants like the bars'. Each is a default Anton can
    overturn.
38. **The Athletics bar above every man** (step 46, Claude's calls on Anton's ask of 2026-10-17 - AI_NOTES "Step 46"; Anton can
    overturn any): (a) ONE view - step 44's - drawing up to two bars per man (one walk, one projection per man, one pool, one cap),
    not a second view; (b) a FIXED height: the fever bar's place is kept under the Athletics bar whenever fever bars can show for
    his side, so the bar never jumps - only with the fever bars off for that side does it sit on the point; (c) the reading = the
    strip's / player bar's: points ÷ his FULL pool, the wound cap dark red like the player bar, BarMath's band colours; no peak tick;
    (d) every man the logic tracks - riders (1 m higher, a plumbing constant `RiderLiftMetres`), heroes, lords - never you; (e) the
    enemy's on by default (`AthleticsMarksShowEnemy`); (f) ONE shared cap, `FeverMarkMax`, kept under its old key (men marked, not
    bars - no config migration) rather than renamed; (g) the pick sorts the candidates first and projects nearest first until the cap
    is full (an off-screen man still never takes a slot - step 44's fix - and ~200 projections a frame instead of one per man on the
    field); (h) the view is named "ALT bars" in the log (it was "fever marks"), its classes keep their step-44 names; (i) the layer
    stays while either bar can show - `FeverEnabled` off no longer hides it while the Athletics bars are on. Also in step 46
    (Anton's playtest): `CostPerBlow` 3 → 7 in defaults.json (config format 9 moves a format-8 file's 3 once; a tuned value kept).
39. **The impact spike** (step 47, IMPACT I1 - docs/IMPACT.md §7, approved by Anton 2026-10-17 with every recommended answer of its §8;
    Claude's calls - AI_NOTES "Step 47"): (a) ONE Advanced switch `ImpactSpike` (off) - a test tool, not gameplay; its loud values
    (x3 stun, cap 2 s, speed 0.5, every 10 s / 20 s, 1 damage) are DOCUMENTED CONSTANTS in Core `SpikeMath`, not settings - it proves
    the engine, it tunes nothing (step 48's model brings the ~60 settings); (b) every lever measured against its OWN CONTROL in the
    same battle: the landed kicks rotate in fours (KnockDown untouched / reel untouched / KnockDown slowed / reel slowed), the block
    stun is x3 for blockers of an EVEN agent index only (deterministic, so the worker-thread decorator and the main-thread
    measurement agree without a shared write) - so "x2 measured" is a fact of that battle, not a guess against a remembered number;
    (c) the forced stagger alternates the engine's priority rule (with it / over it) so I4 learns which one the engine takes; (d) the
    stagger and our own blow never land on the same block (the stagger first), and a man we act on is never a stun sample; (e) never a
    stagger or our blow on a rider / horse / a man on a siege engine or in water / already down - refused BEFORE any engine call
    (RBM crashed a horse); (f) the levers are the reusable seams of steps 48-53 (`ImpactHooks` in the decorators, `IImpactBody`),
    the spike only their first user; (g) each lever behind its own try - an error disables that lever for the mission, the others
    run on; (h) master switch first: off = the game's stun cap back at the next tick (the decorators already vanilla), nothing queued
    is played.
40. **The impact model** (step 48, IMPACT I2 - Anton approved IMPACT §8's recommendations 2026-10-17 night and asked for everything
    overnight; Claude's calls - AI_NOTES "Step 48"): (a) **`ImpactShadowMode` ships OFF** - a deviation from IMPACT §7 (I2 planned
    shadow on until I3): Anton could not playtest the spike first and wants to see the system live in the morning; every effect step
    (49-51) keeps its own family switch and the shadow switch turns ALL effects into logging only, so one checkbox gives him I2's
    shadow battle back (effect steps renumbered 50-52 when step 49 was inserted); (b) `ImpactDamageWeight` ships at 0 (IMPACT §8 q1 recommended (a) at 1.0 - but the sentence was unfinished;
    the brief keeps it 0 until Anton says what he meant); (c) all 60 IMPACT §5 settings in ONE MCM group "Impact: staggers, shield
    drops, knockdowns", placed after "Aggression and fever (AI)" (the combat-behaviour groups together, before the refill and the HUD),
    ordered for the player: the switches, the ceilings (his anchors), the lengths, the limits, who - the model's inner numbers last;
    (d) one decision per event: a landed blow is decided in the damage decorator's KnockDown seam (the only managed place before the
    engine registers it - step 50 / 52 answer KnockDown there) and OnMeleeHit takes it; a block is decided in OnMeleeHit (its stun by
    tiredness in step 51 depends on T alone, so the stun seam on a worker thread needs no shared state); (e) the state is kept in
    shadow too (what WOULD have happened: on the ground, reeling, immune, halved) so the summary's limits read as they would live; the
    shadow reel / ground times use plumbing estimates (`ImpactMath.VanillaReelSeconds` 0.45, `VanillaFallRiseSeconds` 1.5) until
    steps 50-52 set the measured ends (step 50 does for the kicks' reel and get-up); (f) a kick's lengthened reel (T > 0) counts as an impact stagger (the reeling bonus and the
    stagger immunity) - a fresh man's vanilla knock-back does not, so no kick-chain on the fresh; (g) the riposte is never asked from a
    man on the ground, nor when the defender himself was staggered or knocked down; (h) the spike (`ImpactSpike`) forces the model to
    shadow while it runs (it owns the levers); (i) IMPACT §4's worked examples are unit tests (the step-45 script's inputs: a sword 1.2 kg
    at speed 92, the king's at 95, an axe 3 kg at 80, a stick 0.6 kg at 95, a mace 1.8 kg at 85); the doc's 75 % row read 0 where the
    model gives 0.6 % (a wrong-side block) - corrected to 1 in IMPACT §4.

41. **Courage and cohesion** (step 49, Claude's calls on Anton's ask of 2026-10-17 night + his three additions - AI_NOTES "Step 49"; Anton
    can overturn any): (a) toughness = armour AND level AND health, the weights tuned so a looter (8, level 5) is 1.35 and a legionary (110,
    level 26) 5.97 = 4.4 looters (his "4-5:1"); (b) a two-hander / polearm without a shield read from the KIT once (not the wielded slot - a
    native read per neighbour per look), and it weighs only as an enemy; (c) the base odds 1.0 (even) - at 1.0 equal lines fight and the
    envelopment's critical mass comes out at ~2:1 overall (worked example 3), so no separate "flank" threshold; (d) timid men HOLD among
    their mates and WALK only when far and alone - a line's end man never walks into his own line; (e) the walk finishes before courage comes
    back (a lone man out of reach is not yet back with his group); (f) the regroup sprint has its OWN reserve (50%) instead of his order's,
    a panic 0%; it does not end at 3 m from an enemy; (g) the weights only for TIMID men, re-applied at every look (that IS the re-apply after
    each order); `AiMoveEnemySideTimeValue` not used (native-only, meaning unknown); (h) bracing men may be timid (they back off with their
    guard up); fever lowers the odds needed, it does not exempt; (i) riders, a siege's attackers, no-formation men and walled men are not
    looked at; (j) courage works without Athletics; (k) shock = landed hits (OnAgentHit) now, impact outcomes once an impact family applies;
    a shocked man needs twice the mates and half the radius (the flock packs in); (l) the flock target = his side's centroid within 20 m
    when 6+ men are there - one extra query, only for walking men; (m) step 49b (the manager's review): an unshocked man in a fever presses
    whatever the odds (the first clash must happen), shock beats fever; a 5% × bar surge per look for timid unshocked men among their mates
    with an enemy within 8 m, 4 s brave; Melee at full within his reach.

42. **Kicks and bashes live** (step 50, IMPACT I3 - Anton approved IMPACT with every recommended answer and asked for it overnight;
    Claude's calls - AI_NOTES "Step 50"): (a) a family decided LIVE whose effect is not built yet (blocks, clean hits in step 50) decides
    on COPIES of the records (`ImpactRules.KeepsState`) - step 48 wrote every decision's state, which would now give live kicks a reeling
    bonus / immunity / halving (and courage a shock) from staggers that never happened; shadow mode still keeps every state ("would"
    alike); (b) riders: NOTHING of a kick applies (not even the shield) until step 53 decides the dismount - decided, logged, record
    untouched; (c) the shield drop waits for the TICK (the brief: never from inside a hit callback - the spike queued it from the
    callback) and for his own release / bash to pass (vanilla's drop key refuses then; up to 1 s, else not applied); (d)
    `ShieldPickupLockSeconds` is applied through a decorator of the game's `ItemPickupModel` (RESEARCH §R5.4 - only the AI asks it; the
    player's use key has no model, so the lock never touches you); default 0 = vanilla (answer 3); (e) the get-up factor slows the WHOLE
    fall-and-rise (the Fall action and its _continue - the spike's lever 3; the engine gives no separate "rise" action to slow alone);
    (f) a knockdown the dice gave that could not be applied (decided outside the decorator's seam - another mod's damage model on top -
    or its lever disabled) leaves no ground time or immunity (`GotUp(now, 0)`), and a knockdown whose Fall never showed within 1 s is
    "up" at once - the watch keeps the record honest; (g) one watch per man (a newer kick replaces the running one); fresh men's reels
    (factor 1) are watched unslowed as the control for "asked vs measured"; (h) every kick YOU deal or take is logged (not verbose) - they
    are few, and D19 needs them; the on-screen message is step 53's.

43. **Blocks and the riposte live** (step 51, IMPACT I4 - Anton approved IMPACT with every recommended answer, "get all done by morning,
    decide open questions"; Claude's calls - AI_NOTES "Step 51"): (a) the block stun has NO die (IMPACT §3.7's table is its mean): the game
    asks it on worker threads BEFORE the decision, so it is computed from the blocker's tiredness alone and the decision logs the same
    number - the randomness of a tired block lives in the rolled stagger; (b) T for the seam = a per-man float the main thread writes every
    tick (one tick old at most; riders 0 until step 53; you x `ImpactOnPlayerPercent`); (c) the game's stun is held at the game's own cap
    BEFORE it is lengthened (a fresh man's block stays exactly vanilla; an exhausted man's 0.8 s → 1.6 s, not 1.2 → 2.4); (d) the attacker's
    recoil stays the game's - his longer recoil is the riposte, a forced stagger from the tick (the brief), because its dice are rolled on the
    main thread after the seam; (e) the forced stagger asks the engine WITH its priority, then OVER it (the spike's D16 verdict decides which
    one the game needs; the summary counts both); always `act_stagger_backward` (the attacker in front); (f) its speed = the animation's own
    length ÷ the asked length, the own length LEARNED from the measured staggers (0.9 s - the spike's stand-in, RBM's 0.9-1.0 - until the
    first), clamped 0.25-2; (g) a block's knockdown = our own blow (lever 6), never a crush-through - the spike proved it in the smoke and D16
    reads it in game; (h) `ImpactBlocksEnabled` off + `ImpactRiposteEnabled` on = only the riposte is rolled on a right-side block / parry
    (`ImpactRules.DecidesEvent`); (i) a dice outcome that is not played leaves no reeling / ground time on the record (`EndReel(now, 0)` /
    `GotUp(now, 0)`), the halving and the shock stay (it was decided); (j) our own blow's OnAgentHit is not a courage hit (`ImpactHooks.
    InOwnBlow`) - the knockdown is one shock already; (k) the guard is timed for at most 16 blocks at a time (a quarter of the watch pool),
    only blocks with no outcome, never replacing a running measurement; (l) YOU lines only for blocks that DID something (you block hundreds
    of times a battle); the weapon's name read only then; (m) two EMPTY even men stay at the ceilings (x = 1: the bodies even at the floor, T
    1 - nothing cancels) on a parry, a wrong-side block and with a normal shield; a HEAVY (4 kg) right-side shield against an empty man's
    tired swing (85% animation) is a real guard beyond the even band: 76 / 35% instead of 80 / 40 - kept (the shield's weight counts, the
    tiredness is never cancelled); a wrong-side block is never better than a right one at any bar and clearly worse when tired (90 / 50 /
    40 vs 80 / 0 / 40 at empty) - Anton can ask for the right side at exactly 80 / 40.

44. **Clean hits live, the reeling bonus, momentum** (step 52, IMPACT I5 - Anton asleep, "get all done by morning, decide open questions";
    Claude's calls - AI_NOTES "Step 52"): (a) the clean-hit stagger is the engine's OWN flinch slowed (IMPACT §3.5), never a forced
    stagger - its length the model's (0.6-1.5 s), the speed = the flinch's own length (LEARNED from untouched controls and slowed flinches;
    0.45 s guessed) ÷ that, never faster than the game's (factor ≥ 1); a flinch that never shows (the game shrugged it off, he died) leaves
    no reeling; (b) the shrug-off rule sits in `DecideAgentShrugOffBlow` (checked in the decompile: Mission.CreateMeleeBlow, the main thread
    - no snapshot needed), asked only when the game says "shrugged off", clean melee hits on men on foot only (not kicks - vanilla knocks a
    kick back anyway -, not missiles, not riders); the threshold × (1 − T) with T × `ImpactOnPlayerPercent` for you; the game's knockdown
    / knockback questions then follow vanilla's own rules for the un-shrugged blow; (c) MOMENTUM = the attacker's OWN speed along the line
    to the victim (not the closing speed of both - a man fleeing a charge is not spared; vanilla's `MovementSpeedDamageModifier` was not
    used: it is a damage ratio, 0 for kicks and for blows early in the swing), read with vanilla's own velocity formula
    (`MovementVelocity` turned by `MovementDirectionAsAngle`) - four engine reads per event, main thread; (d) its references are settings
    (walk 2.0 m/s - above the game's 1.8 m/s walk limit, so walking men add nothing; sprint 7.0 m/s - between a run ~4.4 and a fresh sprint
    ~8-9) rather than read per man from the game: the brief's formula needs one walk and one sprint for every man, and a tired man's slower
    run SHOULD hit softer; +60 % at a full sprint (the brief's suggestion); (e) momentum multiplies the impact I of EVERY family (the brief:
    a charging axe into a guard hits harder too) and so the riposte sees it turned round; riders' speed is never counted (step 53) and
    counted apart in the summary; (f) momentum alone never breaks a FRESH line (a sprinting swordsman on a fresh even man: x 0.05, stagger
    < 5 %, no knockdown) - the even band and the tiredness still decide; it is devastating on TIRED men (the smoke: a 40 % line hit once by
    a standing swordsman 6 % down, by a sprinting two-handed axe 36 %; an exhausted line 54 %); (g) exhaustion never cancels on a clean hit
    (G none = 1): two EMPTY even men with swords are at x = 1 - stagger 80 %, down 50 % (a Core test); (h) at most 8 untouched flinches timed
    at once (the controls - blows are many, the 64 watches shared); a hit's stagger REPLACES a running watch on the same man; (i) YOU lines
    only for clean hits that did something (you land hundreds); (j) a knockdown answered on a blow the game had already shrugged off is
    counted apart (`knockdowns answered on a blow the game had shrugged off`) - the shrug-off is decided before the KnockDown question, and
    whether the engine plays a fall on a ShrugOff-flagged blow is for D21 to show.
45. **Riders, the rider's momentum, the on-screen lines** (step 53, IMPACT I6 - Anton asleep, "get all done by morning, decide open
    questions"; Claude's calls - AI_NOTES "Step 53"): (a) a landed blow's dismount is answered in `DecideAgentDismountedByBlow` (checked in the
    decompile: Mission.CreateMeleeBlow 5564, main thread; the engine never asks the KnockDown question for a rider, so this IS the seam); the
    decision is made there ONCE and OnMeleeHit takes it (as the KnockDown seam's); (b) a blocking rider's dismount = our own blow with
    `CanDismount` from the next tick (vanilla sets exactly that flag when its own dismount question says yes; RegisterBlow is the proven
    lever 6) - a rider with no horse by then is refused with no engine call; whether the engine plays it is D22's question; (c) a rider's
    SHIELD drops - yes, at the same chance as on foot, for kicks / bashes AND wrong-side blocks (a horse does not hold a shield for him; the
    queued DropItem is vanilla's drop key, which works on horseback) - riders are rarely kicked, so it is mostly the wrong-side block; (d) a
    rider's get-up after a dismount is the game's own (no slowed get-up: the fall from a saddle is a different action, unproven) - his record
    keeps the model's ground time and the 5 s immunity; a dismount not seen within 2 s takes that back; (e) the block stun applies to riders
    (IMPACT §3.8: "stun multipliers apply" - steps 51-52 had held it at the game's for riders): a stun is the engine's own guard freeze, not
    an action of ours; (f) RIDER MOMENTUM - yes, for every ACTIVE blow of a rider (swing or thrust: a lance jab or a sword cut carries the horse
    alike), at HALF a man's sprint bonus (+30 % at a full gallop): the horse's speed already adds vanilla DAMAGE to a rider's blow, and a
    cavalry charge into a tired line must throw men down without felling the line (the smoke: a fresh rider's sword into a 40 % line hit once -
    standing 3 % down, at a gallop 10 %; an exhausted line 45 % - never above the 50 % ceiling; a fresh line 0) - below a sprinting man on foot
    (21 % in the guide's table); references for horses as settings (walk 3.0 m/s - a horse at a walk; gallop 10.0 m/s - the game's mount
    speed 0.22 × (1 + the horse's speed stat): a 45-speed horse ~10 m/s); a COUCHED LANCE never (a passive attack: the game's own big hit -
    vanilla's couched-lance damage is already the cavalry's one-shot); the horse's own charge (running a man over) is not an event of ours; (g)
    the on-screen lines show only what was APPLIED (never in shadow mode - "(would)" lines would teach the player things that did not happen);
    to you: knocked down / off / shield / riposte - NOT a broken guard on your own block (a tired player's every block would speak); by you:
    down / off / guard broken / shield; your men's: only knocked down / off with a HERO on the receiving end (anything more floods a 500-man
    battle); colours = the game's own combat lines (Mission.cs: delivered #AE4AD9, received #D65252), your men's white; (h) the rate limit is a
    per-mission queue: the first line at once, then at most one per 1.5 s, everything meanwhile merged (3 parts + "(+N more)") - a burst of
    events reads as one line, nothing is lost silently (the summary counts merged / overflow / discarded).
46. **The AI punishes a window** (step 54, IMPACT I7 - §8 q14 recommended "yes, after I5, if the playtest shows the AI wasting windows";
    Anton approved every recommendation and, asleep, asked for it built - "otherwise it's a player-only weapon"; Claude's calls - AI_NOTES
    "Step 54"): (a) the lever is step 43's aggression share (the game's own two attack-decision chances) - no new engine lever, nothing forced:
    the AI still decides, only more eagerly; raised to an ABSOLUTE 100 % (the game's own AI) like the fever's peak, never lowered; (b) checked at
    the FEVER's look (~0.25 s, staggered - the cadence that already reads contact) instead of a per-tick check: a window lasts 0.6-3 s + the 0.5 s
    grace, so the share is raised within ~0.1 s on average; a per-tick target read would be 1000 native calls a tick; (c) the target read is
    gated by a battle-wide "any window open" mark (+ 1 s slack) - a quiet battle reads no target at all; (d) windows only from LIVE outcomes (the
    state the decision really wrote, after the apply's own fixes - a knockdown not answered in the seam takes its ground time back and opens
    none); in shadow none (nothing happened); (e) both sides alike, the player's own windows too (his `ImpactState` is written at
    `ImpactOnPlayerPercent`), never the player as the attacker, never a rider (riders keep the game's decision values - step 43); (f) the TIMID
    man: within 3 m of a reeling / downed target he gets courage's surge clock to the window's end (his spell ended - `CourageEnd.PunishWindow`);
    any man within the range gets the clock (so he does not turn timid mid-window); a shocked man presses too (a melee where everyone was hit
    would waste every window otherwise) - only a PANIC keeps him away; (g) in contact only (an enemy within `FeverContactMetres` 5 m) - beyond it
    the decision chances do not matter; (h) THE PROOF is measured with the punish ON and OFF alike (the OFF row = the AI's own reaction to a
    staggered man): attack starts by the target of the man's last look, its state at the release.
47. **Cavalry flocking** (step 55, Anton 2026-10-18 - his wish, then his refinement the same morning that replaced a "gather to 70 %, rest,
    release" rule with a continuous one; Claude's calls - Anton can overturn any - AI_NOTES "Step 55"): (a) THE LEVER: `Agent.SetScriptedPosition`
    on the rider - vanilla's own way of riding an AI horse (the horse race and the joust drive mounted AI agents with it); not the input hook (a
    rider's move vector is read as throttle and turn by the native rider, a world direction would need our own steering loop every frame) and not
    the weights alone (under a charge a rider has no formation slot, so GoToPos has nothing to go to); fail-safe: a point with no navmesh is not
    sent (the group's centre is tried), a throw = one [error], the rider stays the game's; (b) WHO: riders under a CHARGE order only (both
    controllers) - under hold / move / follow / advance their formation already keeps slots, and the player's own riders are steered by his
    orders; riders mixed into any formation flock with their own kind OF THAT FORMATION; never the player; no naval / tournament / arena; sieges
    yes (a rider on a machine or a ladder is busy); (c) its OWN switch - courage off does not stop it; courage only lends its odds to the timid
    factor (and needs courage on for it); (d) EAGERNESS = the bar (a wound counts through the cap; Anton: "do not hold a flock waiting for
    health"), × 0.5 when timid; NO fever for riders - step 43's aggression and fever stay foot-only (riders keep the game's decision values), so
    a wave's "fresh fever" is the wave window itself: `CavalryFlockWaveSeconds` of the game's own charge before anyone decides again; (e) the
    GROUP counts FLOCKING riders of his kind only - free dancers galloping past are not "with him"; the exhausted share is capped at the kind's
    flockers so a flock can always go in; (f) the FLIP needs the leader's readiness ≥ 0.5 (a group of exhausted riders keeps riding until
    somebody among them has rested a little - Anton's "charges are started by the more rested riders") and 3 s of flocking (the flip's
    hysteresis); the goal vote is LOGGED, not a condition (the heading already bends toward the goal as readiness grows); (g) the flock never
    sits: its lead is at least 4 m/s along the heading; (h) the SLOT is a sunflower (Vogel's spiral) fixed per rider - separation without pairwise
    reads and without per-frame noise; ALIGNMENT is the shared heading + lead; (i) the measurements run ON and OFF from the same census.
48. **Right blocks favour the defender** (step 56, Anton's tournament and battle of 2026-10-18: "a block in the right direction I think got
    me staggered ... make sure right blocks are a possible penalty for the ATTACKER - now I felt them as a penalty for the defender, so it makes
    swinging as much as you can the goal, not the skill"; Claude's calls - Anton can overturn any - AI_NOTES "Step 56", IMPACT §3.5 / §3.6 / §4 H):
    (a) what Anton felt that morning was the SPIKE (ImpactSpike on = the model in shadow, levers 4 / 6 forcing staggers / knockdowns on blockers),
    but the model's own right-block ceilings 80 / 40 would have punished a correct blocker the same way - his point stands; (b) a CORRECT block =
    a right-side shield block OR a parry (the events that can riposte); (c) the blocker's x on it = `CorrectBlockTiredWeightPercent` (25 %) ×
    T_D + the mismatch (+ reeling, + damage) - tiredness weighted LOW, not zero: at 0 an exhausted blocker facing a fresh attacker would
    rest on the body ratio alone, and a quarter keeps a tired man's correct block a little less safe (two empty even men: ≤ 5 % staggered,
    never down; a fresh attacker on an empty guard 8 % right side / 21 % parry - a sword is a worse guard than a 4 kg shield); the ceilings
    30 / 10 (both rows), so even the stick against the axe tops out at 30 / 10 - it STILL staggers (17 % fresh, 26 % at half a bar); (d) the
    riposte = 70 % × S(x_r) × (1 − `RiposteBlockerTiredPercent` × T_D), the parameter at 0: x_r = T_A + the turned mismatch already holds "the
    attacker's tiredness and his impact against the guard's stability"; the blocker's own freshness enters only through his body - two
    empty even men 70 %, two fresh 0-1 %, a fresh attacker into any guard 0; (e) the WRONG side unchanged (90 / 50 / 40, T in full, no
    riposte) - the direction of the block is what pays; (f) the block stun's lengthening is OFF, not removed: `BlockStunTiredExtraPercent` 0
    (the spike's lever 2 FAILED - "the stun does not lengthen what the engine plays"); the seam, the cap and the setting stay for a later
    experiment; at 0 the factor is 1 and the game's stun is untouched (the cap is still raised while blocks are live - harmless: every stun we
    do not lengthen is held at the game's 0.8); (g) config format 10 moves a file still holding 80 / 40 / 80 / 40 / 50 / 100 once, a tuned value
    kept and noted; DESIGN's Default column keeps the initial values; IMPACT §4 A-G stay the step-48 model (their tests pin it).
49. **Routing men sprint away** (step 57, Anton 2026-10-18: "when they are broken and begin to rout, use their available sprint for the first
    get-away"; Claude's calls - AI_NOTES "Step 57"): (a) THE ROUT = the game's own `MissionBehavior.OnAgentFleeing(agent)` - v1.4.8: a man whose
    morale breaks panics (`CommonAIComponent.Panic` → `Mission.OnAgentPanicked`), `MissionAgentPanicHandler.OnPreMissionTick` calls
    `CommonAIComponent.Retreat()` then `Mission.OnAgentFleeing` - its ONLY caller, main thread, once per man; a formation's Retreat ORDER also sets
    `CommonAIComponent.IsRetreating` but never calls it, so an ordered retreat is not a rout (`Agent.IsRunningAway` - the native "performing a
    retreat behaviour" flag - and `IsRetreating()` would catch both, and need polling); (b) started straight from the callback (managed
    bookkeeping; the run factor reaches him through the usual recompute budget); (c) the WHOLE store, no SprintMinStartPercent gate (a broken
    man runs with what he has), nothing below `AiMinSeconds` 0.2 s; (d) never ended within 3 m of an enemy (the order sprint's melee end is for a
    man running IN); (e) a running order / regroup sprint is handed over (`SprintEnd.Routed`) - the rest of his store goes into the get-away;
    (f) behind `SprintAiEnabled` (it is an AI sprint) as well as its own switch; never the player (his own key), never a rider (the sprint is on
    foot only); (g) no migration (a new key - a file without it gets the default).
50. **The ALT view decluttered** (step 58, Anton 2026-10-18: "this ALT view is too cluttered. Maybe add those lines as some averages on the group,
    and only show them on up to 5 soldiers that I'm directly looking at and are really close, making it work in tournaments too"; Claude's calls -
    AI_NOTES "Step 58"): (a) "LOOKING AT" = the ANGLE between the combat camera's forward and the line to the bar's point (1 − cos as the pick's key):
    it is the screen's centre at any resolution, aspect (Anton's 3440 × 1440) and field of view, it needs no projection before the pick (only the
    picked are projected - ~5 a frame), and it follows RTS Camera's free camera; the screen distance from the crosshair would need every candidate
    projected first; (b) "REALLY CLOSE" = `AltNearMarksMetres` 15 m from the CAMERA, not the player: the angle is measured from the camera, the view works
    with you down / in the orders view / under a free camera, and the default third-person camera sits ~3 m behind you (15 m = the men in front of you
    and the next rank); (c) a NEW key `AltNearMarksMax` (5) and `FeverMarkMax` KEPT - the every-man view's cap and the near view's ceiling
    (min of the two), its default and meaning unchanged, so no migration; (d) the old view behind `AltMarksEveryone` (off) - a bool, the schema has no
    choice type, and "on = the old view" reads plainly in MCM; (e) NO distance fade in the near view: vanilla's icon fade (0 at 1.5 m, full at 8 m)
    would dim exactly the close opponent you look at; under 1.5 m still skipped; (f) the GROUP: the formation pass counts each formation's men in a
    fever (`FormationAthleticsStats.InFever`) → `fever 12/40` under `ready 34/50`, the ready count's brush and size, the fever bar's orange, shown only
    while someone surges (less noise; it sits last, so nothing jumps); the average Athletics ± spread line is unchanged ("72% ± 8" in the colour of
    their f, the bar under it) - it already reads as the group's average; (g) TOURNAMENTS: there are no formation markers (no MissionFormationMarker
    layer), so the view already copies vanilla's rule (the game's ShowIndicators key, ALT) - Anton's arena log showed it WORKING but marking 140
    "enemy" men: the crowd. Men with no valid team (`Team.IsValid` false - MissionAgentHandler spawns neutral location characters with
    `Team.Invalid`) are now never placed, so in an arena the near bars are the fighters' - your opponent nearest the centre first; teammates in a
    team tournament compete for the same five slots (the enemy switches still apply); (h) no migration: new keys only.
51. **The MCM menu reorganised** (step 59, Anton 2026-10-18: "reorganise the MCM menu - only you now know the details - leave a few options for
    me and the players"; 275 settings in 17 groups were a wall). Claude's calls: (a) TWO MCM PAGES, not one page with a header group - MCM v5 lists
    every registered settings object by its id as its own Mod Options entry (FluentGlobalSettingsContainer keeps them by `Id`), so the player page
    keeps the mod's old id and name ("Trax Combat Enhancements") and every other setting goes to "Trax Combat Enhancements - Advanced", listed right
    under it; (b) the PLAYER PAGE = 20 settings in the master switch + six plainly named groups - what a player reaches for to set up HIS game: the
    master switch; how hard hits land (the damage roll's size - 0 = off, so no separate switch -, the reduction); tiredness (Athletics on / off, the
    swing cost = "how fast men tire" - the one number that scales every bar's length -, the heroes' cost - the hero-centred lean in one number); how
    the AI fights (aggression fresh / exhausted, the fever, the shield wall's hold + its step out, courage, cavalry flocking - each system's own
    switch, never its inner numbers); staggers and knockdowns (on / off + `ImpactOnPlayerPercent` "Against you" - an existing, real difficulty knob;
    no new "how hard" multiplier on the ceilings: 13 per-outcome ceilings are not one clean number, and the step was not to invent systems); the
    sprint + its key; on screen (your bar - the recovery and sprint bars follow it -, the ALT view near / everyone, the on-screen lines); (c) left
    OFF the player page: everything a player would only touch to tune (the costs of kicks / blocks, the brace floors, the step back, the battle pace,
    the impact ceilings, the bar layout, the orders strip, the logging) and the test tools (the spike, shadow mode, verbose logging) - the Advanced
    page, grouped by system as before, its last group "Advanced" (layout, logging, the spike); (d) each setting on EXACTLY ONE page (a test) - a
    setting on both would be two sliders for one value; the player settings moved to new player-page groups (`ParamGroup.Page`), their keys,
    values and behaviour unchanged, so NO config migration; (e) the BUTTONS: "Revert all to defaults" on both pages (it reverts every setting of both
    - a player who broke his game finds it on the short page), "Save current values as a defaults file" on the Advanced page only (a tuning tool);
    each page's MCM Reset resets only its own settings (its own "default" preset); (f) config.json follows the pages: the player page's sections
    first under a `THE MAIN SETTINGS` banner, then `ADVANCED - for tuning`, the header naming both pages; (g) the labels and hints on the player page
    say what a player sees ("Cost per swing", "AI aggression when fresh", "Against you"), and a hint that pointed at a neighbouring setting now
    names the Advanced page's group instead; (h) a NEW setting goes to the Advanced page unless a player needs it to set up his game (CLAUDE.md).
52. **More men fall, and the charge matters** (step 62, Anton 2026-10-19 after his battle of 09:30: "more people falling when troops clash,
    especially when high-tier troops attack low-tier troops ... hits from the BACK ... that first charge sprint + swing must matter - met by a wall
    of shields and blocked the right way, the ATTACKER is staggered ... even a chance to fall back ... the critical not capped when the unit
    sprints ... even for the player"). Claude's calls: (a) THE LOST FALLS first - his log had 35 clean-hit knockdowns decided, 7 Falls seen, 13
    not seen, 14 answered on a blow the game had SHRUGGED OFF: `Mission.CreateMeleeBlow` asks the shrug-off FIRST (5555), then knock-back (5571),
    then knockdown (5575), and a weapon blow flagged ShrugOff plays no reaction (a kick's does - `Blow.NoIgnore` is set for alternative attacks);
    so the shrug-off question now DECIDES the blow (the attacker = the blow's `OwnerId`) and refuses the shrug-off for a decided knockdown or
    stagger, the KnockDown seam taking the same decision (one per blow); a decided fall still not seen after `ImpactFallBackupSeconds` (0.5) =
    our own knockdown blow (lever 6, proven 10/10) from the tick, once; a blow that KILLS is never decided (`Agent.HandleBlow` plays his death,
    and a 0-damage blow plays nothing - both used to read "not seen"); (b) TOUGHNESS = courage's measure (armour, level, health left - shared code,
    the weights live), R × (t_A ÷ t_D)^0.5: a legionary on a looter x2.1 (one doubling, +0.25 x), strongly, not absolutely; an archer caught in
    melee with his bow in hand x1.3 (a shieldless man's weaker guard already shows as more clean hits - he cannot block); (c) DIRECTION from the
    victim's body facing: behind (> 120°) x2, the side (60-120°) x1.4, landed blows only; (d) THE CHARGE = sprinting now, or the FIRST blow within
    1.5 s after a sprint (the AI's sprint ends 3 m from the enemy, so his log's momentum line had "sprinting 0" of 683 blows) - its speed counts at
    least the sprint reference, the momentum's full x1.6; (e) THE REFLECTED CHARGE: on a correct block the riposte reads his blow WITHOUT its
    momentum and his momentum turned AGAINST him (x2 at a full sprint), the blocker's toughness against his; a charge into a RIGHT-SIDE SHIELD block
    (never a parry - a sword does not stop a running man's mass the way a planted shield does) can THROW HIM BACK, up to 40 % × S(x_r) × his
    charge's share (0 at a walk - a walking man only reels), our own blow on him owned by the shieldman, you too (× "Against you") with the red line;
    the reel grows with the comparison (max(T_A, x_r)); (f) THE UNCAPPED CRITICAL: a charging man's melee blow rolls the full ±50 % upside
    whatever his tiredness (`SprintFullCritical`, the Damage group); (g) THE CAP: the multipliers together at most x8 (and at least 1/8); a fresh,
    tough shieldman facing a charge and blocking right stays near-unshakeable; two fresh even men stay at 0; (h) every new setting on the ADVANCED
    page (14, 275 → 289 - no player-page switch: "Against you" already scales the thrown-back on you, and `RiposteKnockdownMaxPercent` 0 turns it off); no
    existing default changed, so no config migration (format 10 stays).
53. **Falls on the hit, a strong rear blow** (step 63, Anton's battle of 2026-10-19 11:02: falls decided 198 → played by the game 71 at the blow,
    by our backup 126 0.53 s later; from behind 13 % down vs the front 11 %). Claude's calls: (a) THE CAUSE of the late falls: every one of the 126
    was a 0-DAMAGE blow - a FRIEND's (a rear-rank swing into his own front man): `Mission.MeleeHitCallback` cancels a non-enemy's damage
    (`CancelsDamageAndBlocksAttackBecauseOfNonEnemyCase`) but the collision still reads StrikeAgent and asks every model question, and a
    0-damage blow plays no reaction (`Agent.HandleBlow`), so only our backup floored him - along the friend's blow, forward. Every decided fall
    WITH damage was played by the game at the blow (71 of 72, one died), so no KnockBack flag is added: a FRIEND's blow or block is simply never
    decided (`ImpactFacts.NotEnemies`; any event; counted in both summary lines); (b) the backup stays for what the engine still refuses (a 0-damage
    enemy blow, a lost reaction) and comes sooner - `ImpactFallBackupSeconds` 0.5 → 0.2 (config format 11, a tuned value kept); (c) DIRECTION AS
    PRESSURE: a multiplier on R only moves the mismatch term (0.25 a doubling beyond the ±0.5 even band) - between even men almost nothing; a
    landed blow from behind now adds `ImpactRearPressure` 0.5 to x straight (a fresh man struck in the back is pressed like a half-exhausted one -
    "a blow from behind can floor even a fresh man"), from the side `ImpactFlankPressure` 0.2; blocks never; (d) the step-62 multipliers ship at
    x1 (`ImpactRearPercent` / `ImpactFlankPercent` 100 - format 11 moves 200 / 140 once): both would count the direction twice; the keys stay for
    tuning; (e) the pressure is no multiplier - the x8 cap never sees it, the ceilings hold (a clean hit 50 % down / 80 % staggered at most). The
    numbers: two fresh even men - front 0, side 0 % down / 8 % staggered, behind 7 % / 40 % (half a bar: 42 / 74 vs the front's 0 / 21); a
    legionary standing behind a fresh peasant archer 47 % down (step 62: 23 %), charging into his back 50 %, the front unchanged at 1 %.
54. **The wall pulls men back** (step 63, the same battle: wall men in contact more than 1 m ahead of their slots 11.1 %, step 42's battles
    0.6-2.5 %, the farthest 7.8 m - while the leash's own steps ended at 1.27 m at most: the far men were HELD men thrown forward). Claude's calls:
    (a) the root is 53 (a): our backup blows on friends' hits threw front men forward (KnockDown | KnockBack along the friend's blow), friends'
    staggers pushed them - gone with friends' blows; (b) any man still out - an enemy's blow, the press, a late rein - is OUT OF PLACE when held
    (not stepping out) and more than max(L, 1) + 0.25 m ahead (a man reined just past L is not): looked at every 0.1 s and the flag written at each
    look; (c) the engine's flag pulled men back in step 41's battles, but nothing says it does while he fights in contact, so a fallback: once he
    has stood out `ShieldWallPullBackSeconds` (0.5) ABLE TO MOVE (not down, not reeling - a fall has to end first) he is WALKED BACK - a backpedal
    toward his slot facing the enemy through step 16's input hook (its own wish, `WallPull`, the courage walk's channel; it works with Athletics
    off), until within max(L, 1) ÷ 2, at most 4 s a spell; (d) every let-go path ends it (the leash freeing him, a gate, the switch, the master
    switch, he leaves); 0 = the game's flag alone and no extra looks (step 42 exactly); (e) Advanced page, "Battle pace (AI)" (the wall's own two
    settings are on the player page; this is an inner number); (f) the [summary] "wall pulled back" line measures it all: spells after an impact
    / other, ends, THE TIME BACK by cause and by who walked him - the in-contact "> 1 m ahead" share of the "wall slots ON" line is the target
    (back near step 42's 0.6-2.5 %).
55. **The wall's blocks count** (step 64, Anton's looter battles of 2026-10-19 12:44 / 12:47: ~20 shieldmen in a wall + a reserve against ~50
    charging looters - "nobody fell or bounced off the shields"; the guard log at the PEAK: a shieldman blocked 46.3 % - right side 7.3 %, WRONG side
    39.0 %). Claude's calls: (a) WHY: `CorrectSideShieldBlock` is decided natively (the managed code only builds `AttackCollisionData` from the
    engine's struct) - the shield's guard DIRECTION against the attack's. An AI man's guard direction is mostly a dice roll: `SetAiRelatedProperties`
    writes `AiRandomizedDefendDirectionChance` = 1 − (skill ÷ 300 × 0.1 / 0.32 / 0.96 by difficulty)³ - 0.89 for a 150-skill legionary on
    realistic, ~1 on normal (right 1 in 4 by luck) - and a shield wall's poses are fixed (`ArrangementOrder.GetShieldDirectionOfUnit`: the front
    rank DefendDown, inner ranks overhead, the edges left / right), so ~85 % of a fresh shieldman's blocks read wrong and every correct-block rule
    of ours (the riposte, the thrown back, the cheap cost, the refill running on) almost never fired; (b) the rule: a SHIELD block whose attacker
    stands within `ShieldFrontArcDegrees` (60 - the front third of a circle: a raised shield covers what it faces, and a wall's neighbours cover
    the rest) of the blocker's facing counts as a RIGHT-SIDE block for every system of ours, outside it the engine's verdict; (c) the PLAYER too -
    a shield held toward the enemy covers the front in any game sense, and the rules must be the same for both sides ("you and the AI alike" is
    the mod's habit); (d) weapon PARRIES keep the engine's direction - skill still matters there; (e) the arc corrects the GAME's guess, not
    step 30's: a tired AI man's share w = `TiredBlockRandomPercent` × (1 − f) of front blocks keeps the engine's verdict (one stateless roll per
    blow), or an exhausted wall would turn unbreakable (an empty legionary's front block: 40 % down / 90 % staggered as a wrong-side block, 0 / 4 %
    as a right-side one); (f) an untracked attacker (a horse) keeps the engine's verdict - no native read for him; the spike (a test tool) too;
    (g) both verdicts are logged - the [summary] "shield front arc" line, the engine's wrong side by the attacker's angle band (to tune the arc).
56. **The AI charge hits at speed** (step 64, the same battles: "charge sprints 153 men avg 1.39 s; ends: store empty 100, an enemy within 3 m 65";
    14 of 160 decided blows were charges, none sprinting at the blow). Claude's calls: (a) a CHARGE order's sprint and a FEVER sprint run INTO
    contact (`SprintChargeIntoContact`) - no end at 3 m: the end is his FIRST ATTACK (the blow then carries the charge) or standing still 0.5 s
    after he began to move (the press, his own ranks); advance / move sprints still end at 3 m (they position men, they are no charge); (b) a
    charge order's sprint SHORTER than `SprintChargeWaitBelowSeconds` (3 s) WAITS until an enemy is within `SprintChargeWaitMetres` (10 m), then
    sprints: a looter's 2 s floor at x2 → x1.1 of his run covers ~12-15 m - spent 30 m out at the order it was gone before the wall; a fresh
    legionary's 6.5 s covers ~45 m and keeps sprinting at the order (step 37's arrival as one block); (c) the fever sprint starts nearer - 4-8 m
    (was 3-12; config format 12 moves the old values once) - so a small store lasts to the man it aims at; (d) the CARRY: `ImpactChargeCarrySeconds`
    1.5 → 3 (format 12), and an attack BEGUN while charging (sprinting or inside the carry) stays a charge until it ends or its blow is decided - a
    slow swing's blow keeps the charge; the momentum of a carried blow stays the sprint reference (step 62); (e) a RUNNING blow (2-7 m/s) is
    already proportional in both the momentum and the reflected charge (`MomentumShare`) - nothing to change; (f) the worked example (Core
    `Step64ExamplesTests`, MECHANICS §11c): of the looters' blows into a fresh legionary wall, before 2.7 % bounced (7.3 % right blocks, 9 % of
    blows charging); now a looter's FIRST (charging) blow 33 % (thrown back 15 %), every later walking blow 16 %; two fresh even men and a walking
    blow into a right block unchanged.
57. **The AI kicks and bashes** (step 65, Anton 2026-10-19: "make the AI do a few shield bashes and kicks now and then - the game has it but the
    NPCs never use them"). Claude's calls: (a) THE LEVER - the engine performs a kick from the agent's EVENT flags (`EventControlFlag.Kick`, the bit
    vanilla's `MissionMainAgentController` sets for your kick key), and the input is contextual: the kick key while the guard is up is a BASH. The
    native AI's own kick tendency (`AgentDrivenProperties.AiKick`, written by `SetAiRelatedProperties`) has no effect in game (RBM's finding - its
    AiKickBash ships the input way) - so the kick is INJECTED through step 16's `OnAIInputSet` hook, RBM's three phases: a bash holds `DefendUp`
    0.4 s first, then the kick key 0.15 s (+ the guard for a bash), then a COMMIT - his own attack / guard / event input held off and his movement
    stopped until the action has played out (≤ 1.5 s; the native AI would cancel it before the hit frame); (b) WHO: an AI man on foot whose target
    (`GetTargetAgent()`, else his immediate enemy - on foot, an enemy) is within `AiKickReachMetres` 1.6 and `AiKickFacingDegrees` 40 of his
    facing; a shield in his off hand = a bash, else a kick - but a shieldman whose target holds a RAISED shield toward him (within 60°) KICKS (a
    raised shield stops a bash, not a kick - RBM; `AiKickKicksARaisedShield`); (c) HOW OFTEN: `AiKicksPerMinute` 0.1 a man a minute × his
    Athletics skill (50% at 0 → 200% at 300) × his bar (none below 35%, full from 75%) × up to `AiKickTurtleMultiplier` 3 against a TURTLE (his
    target's paid blocks of the last ~4 s, faded, + his guard up now, ÷ 2 blocks) × `AiKickFeverMultiplier` 2 in a fever; at least
    `AiKickCooldownSeconds` 10 between two; the worked rate: 40 men in reach, half looter-like, a quarter of the targets turtling - ~5-6 a minute
    ("a few a minute, not constant"); (d) THE LOOK: every AI man every ~0.25 s (staggered); the cheap gates on our own records first (cooldown,
    bar, the Athletics for the cost, a stored swing, staggered / down, busy in his own attack / kick / step back), then ONE die against his best
    case - only a man under it reads the engine (horse, rout, hands, target) and the exact chance takes the same die; (e) THE COST is step 18's: the
    kick is SEEN by the action poll like yours (`CostPerKickOrBash` + a stored swing, step 32) - no attempt without them; needs `AthleticsEnabled`
    (the cost and the sight of the action); (f) never you, never a rider; (g) measured: asked → PLAYED (the action seen) → landed / into a shield,
    by kind and reason (turtle / fever), why not, the Athletics spent.
58. **The kicked man braces** (step 65, the same day: "make sure a looter kicking a heavy shielded troop doesn't do too much - maybe now and then a
    lucky kick can stagger him for a fast bit"). Claude's calls: (a) the brace is part of the impact model's ONE decision of a landed kick / bash -
    no parallel scale: the failure chance = S((x − `KickBraceFromPressure`) ÷ (`KickBraceToPressure` − from)) on the kick's OWN pressure x (his
    tiredness T_D, both men's points, step 62's toughness - armour, rank, health -, skill, a bash's shield weight, reeling, a blow from behind);
    (b) −0.7 / 1.0 calibrated on Anton's case: a fresh looter's kick on a fresh legionary (x −0.32) fails 12.9% - a short reel one time in eight,
    0 falls, 0 shields - two fresh even men brace ~63%, a legionary at half his bar ~51%; (c) a SPENT man (below `KickBraceMinBarPercent` 20 of his
    bar) never braces - step 50 in full; (d) BRACED: no knockdown, no shield drop (refused "braced"), the game's KNOCK-BACK refused - the damage
    decorator's `DecideAgentKnockedBackByBlow` (vanilla: always YES for a kick; the engine asks it after the shrug-off question and before the
    knockdown one, so the model decides the blow there and the KnockDown seam takes the same decision) - and, since a kick's blow is NoIgnore (the
    engine plays a reaction whatever the flags - step 62), whatever reaction still shows is played at 1 ÷ `KickBraceReelPercent` 20 % = 5 x
    (`SetCurrentActionSpeed`, never above 8 x); no reeling state - no chain; (e) FAILED: step 50's rolls as before (0 on a fresh man), the reel ×
    (`KickShortReelPercent` 40 + 60 × T_D %) - a short reel fresh ("a fast bit", 2.5 x speed), step 50's full tired reel empty; (f) you and the AI
    alike - your failure × `ImpactOnPlayerPercent`; an on-screen line for yours ("You brace against X's kick" / "A lucky kick - X staggers for a
    moment"); (g) `KickBraceRefusesKnockBack` off = the knock-back plays, only cut short; (h) measured: braced / short / full / down by the kicked
    man's tier (toughness band) and bar band, the knock-backs refused, the cut reactions seen and timed.
59. **More AI kicks and bashes, each costing more** (step 66, Anton's looter battle of 2026-10-19 14:16 - "ai kicks: asked 0" in 49.4
    man-minutes of contact; why not, of 58154 looks: the dice 44761, too tired 9429, busy 3520; of the 19 reads under the die that found an
    enemy target 18 stood beyond 1.6 m. Anton: "this is actually not a bad idea, for men to kick and bash more, this is a nice way to
    supplement all that killing for more defensive stuff, kicking or bashing, disturbing. Can you make the kick/bash also a bit more taking of
    the energy, say 5 pts each", and "and shield bashes, maybe make them as frequent as swings when the man has a shield"). Claude's calls:
    (a) `CostPerKickOrBash` 3 → **5** for everyone, you too, the hero / leader multipliers kept (3.75 / 2.81); (b) THE WAIT FOR REACH: step 65's
    rate was per minute of LOOK time and only a die that came while the enemy stood within 1.6 m turned into a kick - men fight at a weapon's
    length, so most never did; a decision with the enemy he fights within `AiKickWaitMetres` 5 (the fever's contact) WAITS up to
    `AiKickWaitSeconds` 3 for reach (no new die, the engine read each look) - the rates are per minute IN CONTACT; (c) A MAN WITHOUT A SHIELD
    KICKS at `AiKicksPerMinute` **1.2** × skill × bar × turtle × fever, `AiKickCooldownSeconds` **8** apart - a fresh average man (skill 100)
    ~1.0 a minute of contact, a looter 0.71, a legionary 1.17; half a bar 0.28 / 0.48; vs a full turtle 1.78 / 2.67; in a fever 1.29 / 2.02;
    (d) A SHIELDMAN BASHES WITH HIS SWINGS (Anton's "as frequent as swings"): based on his OWN swings, not a number a minute - each melee
    release of an AI shield-infantry man on foot (step 21's class) earns `AiBashShareOfSwingsPercent` 100 % of a bash (at most 2 saved), spent
    within ~`AiBashDelaySeconds` 1.5 s ÷ the bar factor, `AiBashCooldownSeconds` **2** apart (the kicks' 8 s would cap the bashes below his
    swings); no skill / turtle / fever factor - his swings already carry them; a shieldman never kicks at the per-minute rate (his kicks are
    bashes turned on a raised shield); (e) the bar gate stays at **35 %** (`AiKickMinBarPercent`): the model's seeded walk (Step66ExamplesTests,
    his swings from the fever proof) - 100-75 % 0.95 bashes a swing (7.1 swings, 6.7 bashes a minute; the bashes' own time takes ~1 swing in
    8), 75-50 % 0.60 (13.8 / 8.3), 50-25 % 0.20 (10.8 / 2.2), below 35 % none; the cost of 5 already drains a bashing man (~35 points a minute
    fresh) and the bar factor slows his bashes from 75 % down - a higher gate would cut them where Anton wants them, a lower one would let
    spent men bash; the swing stack (step 32) never starves his swings at the top of the bar (refill ~1 s, the stack ~100 % full); (f) THE
    WORRY (said to Anton): as often as swings is a lot - a fresh shield line spends half its actions bashing, its damage per minute drops by
    roughly an eighth and it tires faster; if the lines look busy rather than fighting, `AiBashShareOfSwingsPercent` 50 (one bash in two swings)
    is the knob, `AiBashDelaySeconds` the softer one; (g) config format 13 moves a file's 3 / 0.1 / 10 once, a tuned value kept and said;
    (h) measured: the [summary] line "ai kicks - bashes per swing and the wait for reach" (the shieldmen's swings, the bashes they earned / lost
    at the cap, the bashes asked a swing; the waits started → kicked / ran out / dropped).
60. **Morale eyes + the fear flock** (step 67, Anton 2026-10-19: "give me eyes on their morale too: ALT to see the average morale of the whole
    group, and the same 5 closest guys' morale too"; "a system where, as troops lose their morale and freshness, they tend to flock less to their
    formation position and more to the centre of their unit's mass, but pushed back away from the enemy mass ... increase their wanting to go that
    way until they are with their people around them on all sides, thus stepping behind their comrades. If their comrade is fresh they will keep
    that position for a while, but as the mass loses morale and freshness they flock more and more into that mass. If they are far away, or
    sometimes when not running, use the sprint ... it will also make it harder for non-professional men to keep a thin line"). Claude's calls:
    (a) FEAR = two dangers, 1 − (1 − m)(1 − t): either frightens a man, both more - a sum would pass 1, a product would need both; (b) DISCIPLINE
    by troop LEVEL alone (0 to level 6, 70 % from level 26, a hero 70 %): armour is courage's (iron makes a man brave in the fight), discipline is
    drill; at 70 % a spent legionary at morale 20 feels 0.30 - the pull 0.23, a quarter of the way: veterans hold until truly spent, 100 % would
    freeze elites; (c) the unit's share 50 %: the mass moves together, one shaken man does not run while his mates stand; (d) THE THIN LINE only
    with an enemy within 8 m and nobody behind him, × (1 − d): a fresh looter rank facing an enemy gets 0.25 (the pull 0.15 - its ends draw in), a
    legionary 0.075 (nothing); (e) THE LEVER is courage's input (a fifth wish), NOT `SetScriptedPosition` (step 55's rider lever): a scripted walk
    turns a man's back (step 16 left it for that), the input keeps him facing his AI's target, his guard and attacks his own; standing = 0, 0; the
    behaviour weights are not touched (the input owns his feet; courage still tightens a timid man's); (f) WHO STEERS: backpedal > wall pull >
    courage's walk back > the flock; a fever press or a surge ends a spell (courage's 49b rules hold for the flock, with courage off too); (g) the
    SHELTER starts at the unit's centre (no jump) and only CREEPS back - 6 m a minute, a quarter under a hold / a wall (1.5 m a minute), a man's
    place never more than 12 m from his slot: no endless shuffle; at the extreme the game's own rout takes over; (h) a place within 1 m of his slot
    = his own AI: small fear does nothing visible, and no tug of war with the game's walk to the slot; (i) THE ANCHOR: a comrade between him and the
    nearest enemy with ≥ 60 % of his bar holds him 20 s, once per cover; (j) CHARGES have no slot: a loose mass, radius (1 − p) × 12 m (≥ 1.5 m) -
    the more afraid, the tighter; (k) WALLS hold above morale 30 (the men's mean); below it step 41's held men are let go (`WallVerdict.Breaking`,
    appended - the old verdicts keep their indexes); (l) the sprint reuses courage's regroup sprint above a 50 % reserve, 10 s apart; (m) THE MORALE
    EYES: the bar at the BOTTOM of the stack (the fever bar keeps its place, the Athletics bar its look), blue, the Athletics bar's size; the morale
    line on the nearest 4 formations within 60 m (their marker's distance); "fresh" = the label bar's mean Athletics; (n) all 29 settings on the
    Advanced page (the brief), the flock's 25 in "Courage and cohesion (AI)" (one system with courage), the eyes' 4 in the ALT group; no config
    migration (new keys only - FormatVersion 13 stays); (o) measured: the 7 [summary] "fear flock" lines (settings; shape - spread and depth ÷ width
    ON vs OFF; drift back a minute by order ON vs OFF; cover / nobody behind / alone ON vs OFF; fear, pull and afraid share by discipline band;
    spells, ends, anchors, steps in behind a comrade, sprints; courage's yields, walls broken, the loose mass, near-slot looks, the steering and
    its cost), the [fear] first-time lines, the HUD's morale clauses.

61. **The wall pushes as one, kicks through the impact model, men tire more** (step 68, Toni's 100 v 100 of 2026-10-19,
    battle_terrain_biome_048, 177 s: `wall slots ON` in contact more than 1 m ahead 15.4 %, more than 2 m 5.8 %, the farthest 6.2 m -
    the looter battle before it 0.8 %; the wall ordered forward with F1 then F4 - in v1.4.8's order menu (both layouts) F1 = Movement,
    F4 = ADVANCE; `wall writes: released to vanilla 224 (another arrangement 162, a charge order 62)`; `ai kicks: PLAYED 147 → landed
    on the man 92, into a shield 39` against `impact … kicks 21, shield bashes 12` and `kick brace … 32`; Toni: "they need to hold the
    wall and push the whole wall, not to break up, so your call" and "tire the men more to slow down the combat again a bit after the
    first impact"). Claude's calls:
    (a) THE CAUSE OF THE LEAK - the advance itself: the game's order slot under an advance lies at the enemy's line and every AI man
    walks to it (§2 "the wall pushes as one"); the other candidates do not fit the log - the charge-order releases (62) and arrangement
    changes (162) take men OUT of the samples (only held men are sampled), the fear flock broke no wall (no "the wall broke" release),
    courage never looks at a held wall man (11013 skips), the spells out of place "after an impact" were 4 of 257, and a punish press only
    raises his attack decisions. A man's own kick / bash lunge is real but small (step 63's 1.25 m mark cannot see it) - (e) handles it.
    The log could not split the leak by order (no such line) - step 68 adds it;
    (b) THE FIX - pace the frame, not a new order: a held man of a moving order near the enemy walks to his slot in the line + one
    `ShieldWallAdvanceStepMetres` (1 m) toward the game's order slot. Every man the same step: the line advances as one body at the
    pace its fighting front allows (the frame is relative to the line's own average, so a line moving together reads ~0 ahead, a man
    who runs ahead of it reads ahead); the leash (step 42) still lets a man step out to strike, and keeps the paced frame while he is
    out. 1 m: below ~0.3 m the engine's own stop tolerance may never walk him; 2-3 m lets the line stretch again. Near =
    `ShieldWallAdvanceNearMetres` 10 m between the fronts: the game's own march farther away (its speed limits already keep a
    marching formation together), the pace for the last 10 m and the push; (c) WHICH ORDERS: advance (yours and the AI's), move,
    fall back, follow - "other moving orders that keep the arrangement"; a stop / hold keeps the game's own frame (its order slot
    there is where the man stands); a charge still releases the wall to vanilla's melee (step 41's gate, unchanged); (d) THE LEVER:
    `Agent.TrySetFormationFrame` (public, vanilla's own call in `HumanAIComponent.ParallelUpdateFormationMovement`) with his world
    position moved to the paced point and the game's own direction for his place - written on the main thread right after vanilla's
    update (step 41's timer) and at the staggered looks; no Harmony, no order changed, the player's order stays an advance; (e) BACK
    TO THE LINE AFTER A KICK: his own kick / bash within 2.5 s (or a punish press - step 54's "step-in") makes him out of place from
    `ShieldWallKickReturnMetres` 0.5 m (step 63: 1.25 m) and walks him back at once (no 0.5 s wait), never while the kick plays (its
    input would be fought); (f) STEP 67's RULE KEPT: a wall whose men's mean morale is below `FearWallBreakMorale` still breaks into
    the flock (step 41's verdict `Breaking` comes before the pace - a broken man is let go and his frame given back);
    (g) THE UNDECIDED KICKS - a FRIEND filter: every landed kick / bash on a tracked enemy already reached the one decision (the
    decorator's knock-back / KnockDown seams, else OnMeleeHit - its own fall-back); the gap is `ai kicks`' "landed on the man", which
    counted the FIRST collision of each attempt whoever it hit. The game cancels a friend's blow (step 63) and the model never decides
    it - and a rear-rank man in a wall, his target 1.6 m off past his own front-rank mate, kicks or bashes his mate (the impact
    header's "a FRIEND's blow 734" holds them; it had no per-event split). Not a different blow path: a kick hitting an ARM makes the
    engine skip the callback altogether (`Mission.MeleeHitCallback` - ContinueChecking), never reaching OnMeleeHit; a 0-damage friend's
    kick reaches OnMeleeHit and is named. So: no kick THROUGH a friend - `AiKickFriendClearMetres` 0.5: a man of his own side more
    than 0.2 m past him along the line to the enemy and within 0.5 m of it holds the kick (the decision waits for a clear line, like
    one out of reach; the game's proximity search into a reused list, read only in reach and in front); measured: whom each landed
    kick hit (his target / another enemy / a FRIEND / a horse or untracked) and every landed kick / bash against the decisions with the
    reasons for the rest (`unaccounted` must be 0); (h) KICKS INTO A RAISED SHIELD are NOT decided: IMPACT §3.5's kick row is "a kick /
    bash LANDS" - the shield stopped it, the engine plays a block (no knock-back on the man), and a decision would need an effect of
    its own (our own blow on a blocker - step 51's lever) for an event the design gives none; counted apart in the coverage line - if
    Toni wants a bash into a shield to rock the blocker, it is a new row; (i) THE COSTS (Toni): `CostPerBlow` 7 → **8**, a WRONG-side
    shield block 2 → **5** (step 22's original - a clumsy block tires), a right-side block 1 → **2**; the kick / bash stays 5, the
    weapon parry 2; heroes × 0.75 and party leaders × 0.75 again kept (a blow 8 / 6 / 4.5, a right block 2 / 1.5 / 1.13, a wrong block
    5 / 3.75 / 2.81); a right-side block now costs what a parry does, so a soldier blocking every 1.5 s no longer climbs on a small
    bar (MECHANICS §6); config format 14 moves a file's 7 / 2 / 1 once, a tuned value kept and said; (j) all five new settings on the
    Advanced page (the brief): the wall's four in "Battle pace (AI)" after `ShieldWallPullBackSeconds`, the friend clearance in the
    impact group after `AiBashCooldownSeconds`; all live.

62. **Winners don't hang back** (step 69, Toni's battles 2026-10-19 15:5x and 16:0x - the second the 100 v 100 on
    battle_terrain_biome_048, 279 s; his screenshot: a few of his men standing 10-20 m off while his charging infantry finished the
    enemy - on a second look perhaps skirmishers throwing / a crossbowman; he loved the looters flocking and backing up. The log:
    `fear flock by discipline: green … fear 0.31, pull 0.30, afraid 67.3%`, `sprints back 222 of 235 asked (… by chance 163 …)`,
    `flocking looks by order hold / move 5825, charge 24269`; `courage by local odds, in contact: >= 2: 2% timid of 21063`). Claude's
    calls: (a) THE GAP: fear was morale × tiredness × discipline - never the fight around him; a tired green winner (green 67 % afraid)
    kept his shelter behind the centre and, on a charge, his leash of 12 m × (1 − p); (b) WINNING = courage's OWN local odds
    (toughness-weighted, the same look, no new scan): 0 at even (1:1) - "at even odds unchanged" -, 1 at 2:1 ("clear odds", a setting),
    a line between; plus the enemy near him SHAKEN on the fear's own morale lines × 50 % (a routing man's morale is spent - no separate
    rout read per neighbour); (c) LOSING IS UNTOUCHED: below even odds the win is 0 whatever the enemy's morale (Toni: losing men must
    flock exactly as now; the smoke proves every position equal) - only the threat line can change a loser (a man 12 m+ from every enemy);
    (d) THE UNIT'S PICTURE: the unit's mean win over its men IN CONTACT (an enemy in courage's radius - rear men with nobody near would
    read ∞ and dilute a losing front); a man with no enemy in his radius takes it whole, one with his own takes the better of his own and
    the mix by `FearUnitSharePercent` - a winning mass relaxes together, a losing unit gives nothing; the table keeps averaging the fear
    BEFORE the win (step 67's loop unchanged), the shelter follows the pull after it; (e) NO THREAT, NO PULL at 12 m (beyond courage's
    8 m scan one `GetNearbyEnemyAgents` read, only for a man who would be afraid, only while the flock is on); (f) THE MOP-UP: the unit's
    win ≥ 50 % = "few or broken"; no leash, no anchor, no step behind a comrade - the charge takes them; (g) THE IDLE DIAGNOSTIC rides the
    same look (no new poll): his speed from his displacement between two looks (no native velocity read), the enemy from courage's scan
    (one read beyond 8 m, only for a standing man of a charge / advance), counted only past 5 s and then whole; holders in priority -
    ranged (normal) > wall > fear flock steering > courage timid > brace > the game; thresholds are measurement constants (Core
    `IdleMath`), not settings; (h) six Advanced settings in "Courage and cohesion (AI)" after `FearChargeLooseMetres`, all live, new keys
    only - no config migration (FormatVersion 14 stays).

63. **The flock is a wide band, not a point** (step 70, Toni's 30 v 50 looters 2026-10-19 15:59 on battle_terrain_001, 258 s, played on the
    step 67 build; his screenshot: the looter mass facing his shield line a deep, narrow column ~15 men deep; his sketches: NOW a narrow, tall
    blob above a wide line, WANTED a wide, shallow mass along the whole line 4-6 men deep, a few stepping forward to swing and back. The log:
    `depth ÷ width in contact ON 1.44 (depth 3.5 m, width 2.6 m)`, `stepped in behind a comrade 1394 looks`, green `pull 0.61`, `flocking looks
    by order hold / move 146, charge 9705`, the first spell at 10.4 s "no enemy near … order charge; his place 8.0 m from him, the shelter 0.0 m
    behind the unit's centre"). Claude's calls: (a) THE CAUSE: the shelter was a POINT - a hold's place slid toward it along the front as much as
    in depth (a 3-wide column ×(1 − p) narrower), a CHARGE pulled every man beyond (1 − p) × 12 m right onto it - the looters' 54 s approach under
    a charge drew the unit into a ball before contact (step 69's threat line now stops that far out) - and "step in 1 m behind the comrade in
    front" stacked the files; (b) a BAND whose direction is the enemy's front: the enemy the unit's men see (as step 67's "away"), aligned with
    the enemy formation's own facing when it faces the unit (its line, not the line between centres); (c) its width the frontage the unit faces
    (`CachedClosestEnemyFormation`'s `Width`) or its men in ranks of 1 m files, whichever is wider, capped 40 m - the band slides along the
    enemy's front (inside it when narrower, covering it when wider); (d) depth by fear: 4 ranks at a small pull to 6 at the full pull, rounded
    - Toni's 4-6; (e) the band's FRONT: 1.5 m short of the enemy's front pushed back 4 m × the pull (step 67's shelter metres) and creeping back at
    step 67's drift cap - the rear edge follows at the depth; (f) a man keeps his place ALONG the front - his slot's lateral, a charging man's
    own - and is compressed only in DEPTH by his rank in his file (never all onto the rear line); (g) RANKS EXACT: each man's rank in his file
    from the period's positions by his id (O(n²) per unit per 0.5 s - 500 men 250 k comparisons; the smoke's 1000 men 0.20 ms a tick); a slid
    man counted in the file he is BOUND for (the reservation lasts until he arrives - without it the column kept looking full and the target
    files empty, 10 deep in the smoke); (h) SLIDE OUT, not step in: in a file with more men than the ranks the men behind them go to the nearest
    file with room, toward the band's ends on a tie; "step in behind a comrade" only where his file has room; (i) FRONT MEN STRIKE: step 67's
    stand (a zero vector within 0.7 m of his place) pinned a front man with an enemy at his shield - nobody covering him and an enemy within
    2.5 m = let go (his AI steps in and swings), walked back once 2 m ahead of his place (step 42's leash idea); (j) kept: the drift cap, winning
    calms (69), the mop-up, the 1 m dead zone near the slot, the anchor, the exclusions; `FearBandEnabled` off = step 67's point for the A / B;
    (k) the measure: men per occupied 1 m file over the men's real span (the depth in RANKS), the deepest file, the width - mean and p90 - ON vs
    OFF, the files over the ranks; the worked example (Step70Tests): 40 looters 3 wide and 14 deep at pull 0.61 before a 20 m line - step 67's
    point → 1 file of 40 (1 m wide); the band → 9 files, 4.4 men a file, the deepest 5, under a hold and a charge alike.
64. **No lone runner behind the enemy's line** (step 70, the same battle: right after the first charge a few looters came round BEHIND his
    line, alone, far from their formation and mates - one stood next to him on his horse behind his line; Toni: "they will charge, but they
    won't break up like that, 1. so far from their formation and line, and 2. from the rest of their comrades". The log (before step 68/69 -
    no lone-runner measure existed): `courage fever and surges: fever presses overriding timidity 450 looks`; `sprint into contact: 168 (ended
    by his first attack 88, he stopped 61)`; `fever sprints: started 38`; `courage isolated … 9 s`). Claude's calls: (a) THE SUSPECTS, in the
    code: step 49b's FEVER PRESS returns before courage's decision - a feverish man alone is never walked back (the isolation rule never ran);
    step 64's SPRINT INTO CONTACT ends only at his first attack (or standing still) - a sprint aimed at a target behind the front (you on a
    horse, a hero) runs through a gap until he swings at it; the game picks the target - our code cannot see a target choice, so the
    diagnostic reads `GetTargetAgent` for lone men only; (b) the DIAGNOSTIC first (man-seconds and men by cause, first that holds: sprint,
    punish, surge, fever, chase, game; the target behind the front by whom), ON vs OFF by the fix switch - Toni's next log says which cause
    dominates; (c) ALONE = courage's own circle (fewer than 2 mates, an enemy within 8 m - the existing `FearMath.AloneMates` / the 8 m scan);
    OUT = 4 m ahead of his unit's front (its men's mean + √3 σ along the band's direction - a man at the front of a charge is not "out") or 1 m
    past the enemy's front (the enemy the unit sees) - the geometry is the band's frame (one per unit, no new reads); (d) FIX 1: the fever no
    longer overrides being ALONE (with mates he still presses: the first clash happens); FIX 2: a sprint into contact ends alone or past the
    enemy's front (checked every 0.25 s from the last look's "alone" and his position - a mass's sprint keeps its punch); FIX 3: a lone runner
    walks back - courage's walk whatever the odds (`CourageMind.Look(…, lone)`), a surge / punish step-in cancelled, the fever never turning him
    round halfway (`LoneMind.Forced` until the walk ends); (e) the walk's "go round the enemy" side chosen ONCE per walk
    (`CourageMath.Direction(…, ref side)`): a man whose group stands straight behind the enemy's line flipped sides look by look and went
    nowhere (the smoke); (f) THE EXCEPTION: a lone man chasing the BEATEN (his side's local odds at `FearWinClearOddsPercent` AND the enemy near
    him shaken 0.5+) is left alone - the odds alone are not enough (courage weighs an enemy at 5.7 m at 11 %: a looter beside one legionary read
    "winning" in the smoke); (g) never you, never riders (courage's gate); courage off = no walk (it measures only) - fixes 1 and 2 still run;
    (h) 11 Advanced settings in "Courage and cohesion (AI)" after `FearMopUpWinPercent` (9 band / strike, 2 lone runner), all live, new keys
    only - no config migration (FormatVersion 14 stays).
65. **Men tire faster still** (step 71, Toni 2026-10-19: "bump up how fast they tire, back to 10 for the swings and suggest for the rest
    tiring moves, they need to tire faster still"). Claude's calls (the manager's numbers): (a) every paid move a step up, the swing the
    most - `CostPerBlow` 8 → **10** (step 5c's original), `CostPerKickOrBash` 5 → **6**, `CostPerWrongSideShieldBlock` 5 → **6**,
    `CostPerShieldBlock` 2 → **3**, `CostPerWeaponParry` 2 → **3** - the right-side block and the parry stay equal, the wrong-side block and
    the kick stay twice them; (b) heroes × 0.75 and party leaders × 0.75 again kept (a blow 10 / 7.5 / 5.63, a kick or a wrong-side block 6 /
    4.5 / 3.38, a right block or a parry 3 / 2.25 / 1.69); (c) the refill unchanged (`FullRegenSecondsStanding` 60) - a soldier blocking every
    1.5 s now sinks on any bar below 120 points (MECHANICS §6); (d) config format 15 moves each untouched old value once (the swing's 8, the
    blocks' 5 / 2 - format 14's -, the kick's 5 - format 13+ -, the parry's 2 - any format), a hand-set value kept and said once; a default
    that did not move says nothing (`MoveOrKeepIfMoved`); (e) the player page's *Cost per swing* hint: "At 10 a recruit (a 50-point bar) is
    empty after 5 blows, a legionary (130) after 13".
66. **No accidental charge orders** (step 72, Toni 2026-10-19: placing a unit with a click or a click-and-drag, the cursor crossing another
    unit's floating banner turned the move into a charge at that unit - his men sprinted off and wasted their sprint; "the unit only goes after
    another unit on charge when my cursor is directly over their icon and when I click it, not click and drag"). What vanilla does (v1.4.8,
    decompiled): `MissionFormationTargetSelectionHandler` FOCUSES every enemy formation within 10 m of the camera, else the one whose median
    point 3 m up projects nearest the SCREEN CENTRE within 70 px × height / 1080 (not the mouse); `GauntletOrderUIHandler.TickInput` charges
    the first focused unit on ANY left-button release in the orders menu (`ChargeVisualOrder` → `SetOrderWithFormation(Charge, unit)`),
    drag or not, and suspends the troop placer - its move is lost. Claude's calls: (a) the lever is the handler's own PUBLIC
    `SetIsFormationTargetingDisabled` - no Harmony, no order re-issued (so no charge, no sprint, no fever ever starts), the placer's move
    with the dragged frontage simply stands; (b) a layerless `MissionView` joined on the logic's first tick - AFTER the handler in the
    behaviour list, so its `OnPreDisplayMissionTick` runs first each frame; it replicates vanilla's focus rule to know the unit a release
    would charge and holds the targeting OFF while the left button is down unless the press was ON that unit's icon
    (`OrderChargeIconPixels` 30 - the icon is 50 across), the cursor is still on it and no drag (`OrderChargeDragPixels` 12 - the press
    point's screen distance, so a camera turn counts too); the decision of the last held frame stands through the release frame; (c) idle
    = vanilla's targeting untouched, so the F-key charge at the unit you look at still targets it, and the markers' highlight shows as
    vanilla until you press (it goes out the moment a hold would not charge - the feedback); (d) the gamepad (its own confirm path) and
    naval battles (War Sails drives the same switch itself for a captain's ship) stay vanilla; (e) the player's `OnOrderIssued` counts
    what really went out - a targeted charge during a converted click is a FAILURE line; (f) three settings in a new Advanced group
    "Orders: no accidental charges" before "Advanced" - a fix that should simply work, on by default (CLAUDE.md "Which MCM page"); new keys
    only - no config migration (FormatVersion 15 stays).
67. **Line discipline by tier** (step 73, Toni 2026-10-20: 60 tier-5 men against 120 tier-1 recruits - the recruits attacked on a very broad
    front about 2 deep, wider than his whole army, and engulfed him; "I imagine a low tier untrained troop to not be able to keep that
    disciplined line all the way and execute that encircling"; his map {T0 6, T1 5, T2 4, T3 3, T4 2, T5 1, T6 1}; infantry AND archers /
    skirmishers count, cavalry and squares / circles left out; "show me physically that I can't stretch it further"). What vanilla does
    (v1.4.8, decompiled): the AI's behaviours set a formation's width by its FORM ORDER - `BehaviorAdvance`, `BehaviorDefend`, `BehaviorSkirmish`,
    `BehaviorPullBack`, … `SetFormOrder(FormOrder.FormOrderWide)` = round(√(men ÷ 16)) × 16 files (120 men → 48 files, 2.5 deep - Toni's
    front), `BehaviorReserve` / `BehaviorSkirmishLine` / `BehaviorSkirmishBehindFormation` `FormOrderWider` (× 64), some a custom width in
    their `TickOccasionally`; the game re-applies the form order as men fall (`ReapplyFormOrder`). The player's drag (`OrderTroopPlacer` →
    `OrderController.SimulateNewOrderWithPositionAndDirection`) and the move it issues (`MoveToLineSegment` - the same simulation, then
    `SetFormOrder(FormOrderCustom(width))`) both clamp each formation's width to `formation.MaximumWidth` = `Arrangement.MaximumWidth`, a
    VIRTUAL property of `LineFormation`. Claude's calls: (a) THE RULE - a man's ranks = the map at his tier (`GetBattleTier()`: ceil((level − 5)
    ÷ 5) clamped 0-7 - the campaign's troop tier for a soldier, also in custom battles; 7 counts as 6), a HERO as `LineDisciplineHeroTier` 6
    (a lord, a companion, the player: few, and they hold their place); D = the MEAN over the men in the formation's line (detached men out);
    the cap F = ceil(men ÷ D), at least the game's own √men (it never forms narrower) and 1; its width the game's own (F − 1) × (interval +
    a man's width) + a man's width at the formation's current spacing (+ 0.05 m so the game's own floor gives exactly F). A fractional D gives
    a partly filled last rank by itself: 100 men at D 3.5 → 29 files, 3 full ranks + 13 (the 4th 45 % full - Toni's "the 4th at most 1/2");
    120 T1 → D 5 → 24 files × 5; 60 T5 → D 1 → 60 files = never narrowed; 70 T1 + 30 T3 + 20 T5 → D 3.83 → 32 files, 3 ranks + 24.
    (b) THE AI - the lever is the game's own `SetFormOrder(FormOrderCustom(cap width))` (the order the player's own drag gives) in the logic's
    PRE-TICK: the AI's behaviours run in the team tick at the end of the frame's agent job, our pre-tick is the next managed point (step 41's
    timing), before any man reads his slot - a widened line never reaches the men; a line too thin after losses or a new D shrinks back the
    same way (AUTO-SHRINK); D recounted every `LineDisciplinePollSeconds` (1 s) and at once when the formation's count changes, the width
    checked every frame (~20 formations - cheap). No Harmony: the behaviours keep setting their form orders and we narrow the result; a
    clamp the game did not take (a line mid-transformation) is retried after 1 s, not every frame. (c) THE PLAYER - `LineDisciplineForPlayer`
    (on, the PLAYER page: Toni's own toggle) holds his formations (his team, not left to the AI) to the same cap, and `LineDisciplinePreviewCap`
    gives each of them `DisciplinedLineFormation` - the game's plain `LineFormation` whose `MaximumWidth` answers the cap (and whose `Clone`
    keeps its type for the game's simulation formations, which reset whenever the types differ): vanilla's own clamp then stops the drag's
    preview and the move at the cap - the line PHYSICALLY stops growing. Not a game MODEL (the house rule is about `AddModel`): a
    per-formation object the game swaps itself on every arrangement order; ours goes in with the game's own `Formation.Rearrange` (a line →
    line transfer hands the unit tables over by reference - nothing rebuilt), only over the game's PLAIN line (never a column's
    `TransposedLineFormation` or another mod's), and is put in again after the game's next arrangement order puts its own back; the summary
    counts the times the game asked it and got the cap (the proof the drag read it). A line on screen when his order asked wider
    (`LineDisciplineShowMessage`). (d) Off (the master switch, `LineDisciplineEnabled`, or his switch for his formations) gives every width
    we set back (the game's form order our first clamp replaced, if ours still stands) and takes every capped arrangement out. (e) Naval
    battles never (ship decks); sieges off by default (`LineDisciplineInSieges` - the walls and streets set the width). (f) 15 settings: the
    player's switch on the PLAYER page (21 settings - the page's pinned limit 20 → 21 on purpose: a system the player feels on every drag;
    the system's own switch stays Advanced - the AI should simply keep to it), the rest in a new Advanced group "Line discipline by tier"
    before "Advanced"; new keys only - no config migration (FormatVersion 15 stays). (g) Step 67's fear flock and step 70's band keep
    working inside the new shape: they move men toward a band / a shelter by their own input wish, from the slots the narrowed line gives -
    a deeper block simply has more ranks to compress.
68. **The green block stays a block into contact** (step 74, Toni's battle 2026-10-20 20:30: 180 T1 recruits held ~5 deep by step 73 advanced
    as a block, then just before contact spread into a wide loose crowd with a trail wrapping round his right flank along a rock; AI_NOTES "Step
    74"). What vanilla does (v1.4.8): `TacticFullScaleAttack` walks the infantry in with `BehaviorAdvance` (a Move to the enemy's front - the slots
    hold the block) and, once the battle is joined (≤ 5 s from the enemy), weights `BehaviorTacticalCharge`, which beats the advance in the last
    seconds and - for an infantry formation at least as wide as the enemy's (× 1.1) - gives `MovementOrderCharge`: the Charge movement state has
    NO slots (`GetOrderPositionOfUnit` returns the order's position, every man's behaviour values are a charge's) and each man runs at his own
    target, the flank men round the enemy's ends, the engine's avoidance and the ground (a rock) shaping a trail. Before it, under ranged fire
    within 80 m, `BehaviorAdvance` turns a shieldless infantry formation to LOOSE (a wider spacing). Claude's calls: (a) THE MAP one rank deeper
    (Toni, mid-step): T0 7 / T1 6 / T2 5 / T3 4 / T4 3 / T5 2 / T6 1 in defaults.json (this table's Default column keeps step 73's initial map),
    config format 16 moving only untouched values - 120 recruits at most 20 files, 60 veterans 30, elites never narrowed. (b) THE HOLD - a formation
    step 73 holds with D above 1 keeps its BLOCK under a charge (always) and an advance / move / hold within `LineBlockNearMetres`: in its frame (u
    toward the enemy - the fear band's direction, else its facing; the centre line the MEDIAN of its men across it, so a trail cannot drag it; its
    depth ceil(men ÷ files) ranks), a man beyond the cap's half-width + `LineBlockSlackMetres` across, `LineBlockAheadMetres` past its front with no
    enemy in reach, or the slack behind its rear is WALKED BACK through step 16's input hook (a sixth wish, his own facing, guard and swings kept -
    never scripted movement that turns his back), his place moving with the block and leading it by `LineBlockLeadMetres` - no stall: the men
    inside keep the game's charge, the block arrives at its own speed. (c) IT SCALES WITH D: the hold s = (D − 1) ÷ (`LineBlockFullAtDepth` − 1),
    the room + (1 − s) × `LineBlockLooseMetres` - recruits (D 6) and trained men held fully, veterans (D 2) half, elites (D 1) free. (d) THE FIGHT:
    a man with an enemy within `LineBlockStrikeReachMetres` fights unless he stands more than `LineBlockStrikeLeashMetres` beyond the room (the
    front and the edge fight, a man round the flank behind the enemy is walked back); the block lets go when its men win
    `LineBlockMopUpWinPercent` (step 69's win - the enemy beaten). (e) PRIORITY: a step back's backpedal > the wall's pull > courage's walk back >
    the fear flock > the block hold (the last wish; a look of a man courage walks or the flock steers yields) - and the fear flock's band is
    clamped INSIDE the block (never wider than the cap, never past its sides), courage's targets lie inside it; a surge, a punish step-in, a
    fever press or a sprint is free inside the block and within strike reach, the block's vector takes his movement only beyond the room; the
    lone-runner fix (step 70) still walks a man alone behind the enemy's front home; walls hold their own. (f) Both sides, under step 73's
    switches (the master switch first, `LineDisciplineEnabled`, the player's formations by `LineDisciplineForPlayer`) and its own
    `LineBlockHoldEnabled`; never the player himself. (g) THE DIAGNOSTIC (measurement only): per formation step 73 holds, every 0.5 s its
    movement order, active behaviour, arrangement, width (10th-90th / all), depth, the game's width vs the cap and the share of men more than 3 m
    from their slots, binned by the time from its first contact; every order / behaviour change logged as it happens - the next battle names
    the cause whichever it is. 11 Advanced settings in "Line discipline by tier"; new keys need no migration.

69. **A standing wall stands firm** (step 75, Toni 2026-10-22: "the men in the shield wall still step forward even when on a standing
    order, they take a few steps to strike at an enemy, then fall back. Can you make a trigger in the settings that I can test on and off
    ... they do not leave the wall no matter what. They strike from their place if they can"). (a) WHAT MOVED THEM, each lever and how it
    is closed for a man standing firm: step 42's LEASH (a held man freed to step up to 1 m at an enemy close in front - exactly Toni's "a few
    steps to strike, then fall back") → 0, each step it would give counted as refused; THE GAME'S FRAME under a Stop (`Formation.GetOrder
    PositionOfUnit` returns his OWN position for `StandGround`, so the engine never walks a drifted man back; vanilla also writes no
    integrity data under a Stop) → the frame pinned to his slot (`Agent.TrySetFormationFrame`, step 68's call, right after vanilla's
    update); THE INTEGRITY TOLERANCE (vanilla hands the engine the formation's own spread of positions with the flag - its native use is
    unseen) → at most the slack; step 63's mark (1.25 m) and step 68's kick return (0.5 m) let a lunge, a shove or the game's approach
    stand → out of place from the slack (0.3 m), walked back at once whatever moved him, never while his own kick plays; a running AI
    sprint (a move order's) → ended; step 68's ADVANCE PACE (a move pushed back from its order slot was paced 1 m toward it) → his slot
    instead. Already closed before this step for any held man: courage's surge and the fever press (courage never looks at a held man),
    the fear flock and the band, the block hold (they skip him), the step back (refused in a holding arrangement), the fever and its
    sprint (a hold / move order ends the fever). The punish raise (step 54) only raises the attack decision - he swings from his slot;
    its step-in is courage's. (b) WHAT STAYS: his attack input is never touched - guard, blocks, swings at men within his weapon's reach,
    step 65's kicks and bashes in reach (1.6 m; a kick's lunge is walked back after it plays); step 63's pull of a man thrown out; step
    67's morale break (`FearWallBreakMorale`) and step 41's escape (far behind) still let go. (c) "AT ITS PLACE": a Stop at once; a Move
    once `Formation.CurrentPosition` is within `ShieldWallStandFirmArrivedMetres` (2) of `Formation.OrderPosition` and the men's average
    velocity at most `ShieldWallStandFirmStillMetresPerSecond` (0.5) - latched per man (a fighting line shuffles) until the order position
    moves more than 2 m (a new move) or the formation stands more than 4 m from it (pushed far back: the game walks it home as a line under
    step 41-68's rules, then it stands again); an AI general's Move counts the same (its advance / tactical charge never). Squares and
    circles too (outward = out, as the leash). (d) The switch on the PLAYER page (Toni tests it on and off), on by default; both sides' AI
    formations, never the player himself. Five Advanced numbers / switches in "Battle pace (AI)"; new keys, no migration. (e) UNVERIFIED in
    game: that the engine honours the pinned frame and the tight tolerance, that men in contact still land swings from the slot (the
    summary's strikes a man-minute ON vs OFF), that the walk back from 0.3 m does not look jerky (raise the slack).

70. **Spearmen keep their spears, the second rank thrusts past** (step 76, Toni 2026-10-22: "my shield wall-ers have spears but they keep
    using their swords, even though the spear has a longer reach"; RBM fixes it, and RBM's second rank is effective - its spears not stuck
    in the man in front). (a) THE PREFERENCE - RBM's `WeaponPreference` (its public source read) without Harmony: the game assigns
    `AiWeaponFavorMultiplierMelee / Ranged / Polearm` = 1 in `SetAiRelatedProperties` on every recompute, so the stat decorator writes
    after the base model, never compounding: the polearm x`SpearPolearmFavor` (35) while no enemy is close, the melee x`SpearSidearmFavor`
    (55) once one has been close `SpearSidearmAfterSeconds` (2); back `SpearPolearmBackAfterSeconds` (4; `...MountedSeconds` 2) after the
    last close look; a rider faster than `SpearRidingFastMetresPerSecond` (4) has nothing close. Who: an AI man (both sides, never the
    player or a man he took over) whose four slots hold a melee polearm (a usage of a polearm weapon class) AND a melee non-polearm; a
    thrown / consumable / ranged item is neither (RBM); the loadout re-read only when a slot's item changes. The look - the game's
    proximity search - runs on the main thread from the logic's tick every ~`SpearLookSeconds` (0.5, jittered 0.75-1.25 by his index),
    after deployment; a flip asks the tick's budgeted recompute (`Agent.UpdateAgentProperties`); the decorator only reads his flag (RBM's
    lesson: a proximity query inside the stat model deadlocked `Agent.Build`). (b) CLOSE FROM THE SPEAR'S LENGTH (Toni's addition a):
    `SpearCloseBaseMetres` 1.0 + `SpearCloseLengthPercent` 60 % of his LONGEST melee polearm's `WeaponLength`, clamped `SpearCloseMinMetres`
    1.5 - `SpearCloseMaxMetres` 3.5 - a 1.5 m spear 1.9 m, 2.0 m 2.2 m, 2.5 m 2.5 m, 3.0 m 2.8 m; a length not known (or
    `SpearCloseFromLength` off) = `SpearCloseFallbackMetres` 2.5 (RBM's). A longer pole is clumsy sooner; a short spear is kept closer.
    (c) THE BACK RANK (Toni's addition b): `Agent.GetFormationFileAndRankInfo` (managed: rank 0 = the front, a square's / circle's outer
    ring) and the arrangement's `LineFormation.GetUnit(file - 1 .. file + 1, rank - 1)` (the staggered rank in front) - a man in rank 1+
    with a man of his own in front, in a shield wall / square / circle under any order step 41 holds (not a charge, a retreat, no order),
    or (`SpearBackRankInStandingLine`) a plain line on a STOP or standing firm, keeps the polearm whatever is close: his sidearm spell
    cleared and the polearm favoured AT ONCE. A man in rank 1 of a thinned file (nobody in front) is the front. (d) FACING A POLEARM
    (Toni's addition c, `SpearKeepVsPolearm`): an enemy within the radius wielding a melee polearm himself does not count - a man facing
    only polearms keeps his; one other enemy among them counts. (e) THE FALLBACK, only if favors alone do not hold (`SpearWieldOverride`,
    OFF - nothing offline says they fail; PLAYTEST D41 decides): step 16's `OnAIInputSet` hook turns the AI's own `Wield` event of a
    sidearm slot into the `Wield` of his polearm's slot while the polearm is favoured. (f) THE THRUST (`ThrustsPassFriends`): vanilla's
    `Mission.MeleeHitCallback` cancels a friend's damage (`CancelsDamageAndBlocksAttackBecauseOfNonEnemyCase`), sets the attacker's stun to
    `StunPeriodAttackerFriendlyFire` (0.4) and asks the damage model's `DecideWeaponCollisionReaction`, whose default answers `Bounced` for
    a 0-damage blow; our decorator answers `ContinueChecking` BEFORE the base model for a THRUST (`StrikeType` thrust, not a kick / bash)
    into a FRIEND (`Agent.IsFriendOf`) - everyone's, the player's too; `ThrustsPassPolearmsOnly` (off) = a polearm's only. Swings still
    bounce. The friendly-hit stun (one global ManagedParameters value) is held at `FriendlyHitStunSeconds` 0.1 while the rule is on and
    given back at the battle's end / switched off (step 47's `StunPeriodMax` pattern). (g) RBM's DATA ROUTE is NOT built: its
    `RBM_spear_combat_parameters.xml` (type `animation_combat_parameters`, declared in its `ModuleData/project.mbproj`) starts the polearm
    thrusts' collision check later - `polearm_thrust_ready` / `polearm_thrust_left_ready` / `polearm_upthrust_ready`
    `collision_check_starting_percent` 0.35, `collision_check_ending_percent` 0.55 (vanilla 0.20 / 0.45, 0.20 / 0.45, 0.32 / 0.50) - so
    the spear's first part of the thrust passes through the man in front without a check; the fallback if the engine's ContinueChecking
    does not carry the spear (AI_NOTES "Step 76"). (h) 21 settings (1 player page, 20 Advanced in "Battle pace (AI)"); new keys, no
    migration (FormatVersion stays 16). Gate: `ModEnabled` FIRST, then each switch; off lifts every favor (recomputed) and gives the stun
    back; the looks still measure. (i) UNVERIFIED in game: that the native AI honours the favors (holds the spear), that `ContinueChecking`
    lets the spear reach the enemy behind, whether the friend still flinches, whether the native AI starts thrusts with a mate in front.

71. **Press, don't pass** (step 77, Toni's battles 2026-10-22 10:2x-10:48, scene battle_terrain_b: 108 T1 recruits - D 6, 18 files, 26.6 m -
    against his 59 T5 shield wall - D 2, ~23 m; at first contact the AI's `Charge / BehaviorTacticalCharge`, 65-85% of the recruits out of
    their slots; his screenshots: rear-rank recruits breaking off from their mates and walking round his wall's ENDS and behind it, alone or
    in small groups - "dancing around" - then in through the middle; stand firm (all three options) OFF changed nothing; the log: the fear
    band slid men "out of a full file" 3730 times, its width p90 27.5 m - wider than his wall; the block hold's sideways allowance ±15.3 m
    against his wall's ±11.6 m; the lone-runner fix caught 2 men - these men move in groups; his wall men out of their slots 29% with stand
    firm off. Real men press against the line in front of them; they do not walk past it or round it unless it breaks or their mass is
    genuinely wider and wraps as a body). (a) WHO: every AI man on foot courage looks at (both sides; never you, riders, held wall men, a
    siege's attackers, no formation) whose unit fights an enemy LINE. (b) THE LINE TEST (`PressMath.LineKind`, in this order): at least
    `PressLineMinMen` (8) men looked at in the period; not a retreat; then held by step 41 (half its men looked at are wall-held - standing
    firm included) = a WALL, else a block step 74 holds (counted, D above 1, the block hold on) = a BLOCK, else - under no charge - at least
    `PressLineInSlotPercent` (60) % of the men whose slot was read within 3 m of it (and half its men read) = SLOTS; a square / circle of any
    of those = a RING. (c) THE FRONT SEGMENT (`PressLines`, every 0.5 s after the blocks' frames - courage's look feeds it through the block
    hold's reads, one set of native reads): in the line's facing frame (u = its `CurrentDirection`, e across) its men sorted across; the
    FRONT = the median of each man-wide lateral bin's front-most man (bins `PressManWidthMetres` 1.2 m, each opened at a man - one man out
    of the wall never moves it); its FRONT MEN = within `PressFrontDepthMetres` (1.5) behind it (a rear man stepping up holds it); its ENDS
    = the extreme front men ± half a man; its GAPS = the stretches between neighbouring front men no man covers, `PressGapMetres` (2) or
    wider (up to 8 kept); its DEPTH front to rear; its REAR a second face the same way (an attack from behind presses on the rear rank). A
    ring: its centre and the 90th percentile of its men's distance. (d) HIS LINE: the unit of his nearest enemy in courage's scan when it is
    a line, else his unit's - the line its men's votes name (the most-seen enemy unit that is a line); the unit's FACE (front / rear, by its
    centre against the line's middle, with a hysteresis of half the depth + 1 m), its WIDTH across the line ((q90 − q10) ÷ 0.8 of its men,
    at most a held block's step-73 cap - its body, not its trail), its FRONT (the 10th percentile of its men's d). (e) THE VERDICT
    (`PressMath.Decide`; d = in front of the face, his side positive): his unit winning `PressWinPercent` (50, step 69's unit win) = free
    (the line is beaten); on his side - in a gap = THROUGH, beside an end with a wrapping mass = WRAP, within `PressWatchMetres` (4) =
    CLAMP (a REAR-RANK man - more than `PressFrontDepthMetres` behind his unit's front - of a unit in contact, more than `PressEndSlackMetres`
    (1) sideways past the end: his outward sideways movement clamped too), else far; past the front by more than `PressToleranceMetres`
    (0.5) - a gap = through, beside an end with his mass = wrap, within the line's body (depth + 1 m) = PUSHED straight back out to 0.3 m in
    front at his lateral, behind the whole line - broken (a gap) or wrapping = left, else WALKED ROUND THE NEAREST END (sideways behind it to
    `PressRoundEndMetres` 2 + 1 m beyond the end, then forward to 1 m in front). A ring: inside = pushed out radially, within the watch =
    clamped, no ends. (f) THE WRAP: his unit spans `PressWrapMarginMetres` (6) more than the line's front AND he has `PressWrapMates` (5)
    mates within `PressWrapMatesMetres` (4) - Toni's recruits: 26.6 m (the cap) on 23.6 m = 3 m, never; once a unit wraps a line it keeps
    wrapping it (its flanks squeezing in behind narrow its spread - they are not sent back). (g) THE LEVER: a seventh input wish
    (`AiInputState.Press`) on step 16's component. THE CLAMP: the component of his FINAL movement vector along the direction into the line
    (his own frame, refreshed every 0.1 s, only within 0.6 m of the front - `PressMath.LookaheadMetres`, a 6 m/s run between two steers) is
    taken out - LAST in the filter, whoever wrote the vector (the game's charge, courage's walk, the fear flock, the block hold); his move bits
    out when it bites; he keeps moving along the line and away from it. THE WALK (pushed out / round the end): his movement replaced, facing
    as his AI faces, the stick slower near the place, the ground ahead checked. PRIORITY (decided): a step back's backpedal > the wall's pull >
    THE PRESS'S WALK > courage's walk back > the fear flock > the block hold; the clamp after all of them (inside or behind the enemy is the
    most wrong place to be; the clamp only ever removes motion into the line). His attacks are never touched: he fights whatever is in reach.
    Works with Athletics off. (h) THE BAND (step 70): never wider than its enemy formation's frontage + `PressBandMarginMetres` (1) a side;
    its ranks = enough for all its men in its files (`PressMath.BandRanks` - beyond `FearBandRanksMax`): the surplus DEEPER, never out to
    the sides (Toni's 108 recruits: 27 m in 4 ranks → 25 m in 5). (i) THE BLOCK HOLD (step 74): from its first contact its sideways allowance
    is cut to its enemy line's front ends + `PressBlockEndMetres` (0), at least 2 m wide (`BlockHoldMath.DecideRange` - a symmetric range =
    step 74 exactly): ±15.3 m → ±11.8 m on his wall. (j) THE DIAGNOSTIC (ON and OFF): men who CROSSED a front (more than 1 m past it, on its
    side at the look before) by where (round an end / through the middle) and cause (courage's walk, the fear flock, the block hold, his
    target past the front - one `GetTargetAgent` read per crossing -, the game itself), allowed or not (a gap / a wrap / broken or winning);
    the share of looks past a front; the looks by verdict; clamp / side / walk input frames; the lines by kind, the gaps, the wrapping
    units; the band's clamps and extra ranks; the block's cut looks; cost - the `[press]` first-time lines and the two [summary] "press,
    don't pass" lines. (k) 16 settings: `PressDontPass` on the PLAYER page (a new AI system's switch Toni tests on and off), 15 Advanced in
    "Courage and cohesion (AI)"; new keys, no migration (FormatVersion stays 16). Gate: `ModEnabled` FIRST, then `PressDontPass`; off = every
    wish let go at once, the band and the block back to steps 70 / 74, the looks still measure. (l) UNVERIFIED in game: that the engine
    honours a clamped vector in a charge (as it honours courage's walk and the block's), that the pressing men still fight (strikes in
    contact), that the wall's faces and gaps read right from real men (the [press] lines), the cost in a 1000-man battle.

72. **The battle's rhythm, part 1 - rotation and the lull** (step 78, Toni 2026-10-22, his sketch 1-4: "two packs of dogs - they don't want to
    get hurt but they won't back down, and they won't do reckless things like flanking or breaking up": 1. fresh lines clash at full might; 2. THE
    GRIND - the tired front men stay, neither pushing nor backing, while fresher men from behind come forward to pass them and hit; 3. THE LULL -
    when the surge is spent and nobody routs a distance forms, both back up a bit to catch their breath, the more aggressive the order the more
    they tire first, the most tired go to the back; 4. waves - step 79). (a) WHO: every AI man on foot courage looks at (both sides; never you,
    riders, no formation) and every held wall man (step 41 - through the fear flock's FearSkipped note); both parts need the Athletics system (a
    bar to read). (b) ROTATION (`RotationEnabled`, player page): a FRONT man (an enemy within `RotationContactMetres` 2.5, no mate of his file in
    front of him short of the enemy - `RhythmMath.Covers`) who is TIRED (bar at or below `RotationTiredPercent` 35 or his own brace floor -
    `RotationUseBraceFloor`: charge 20, advance 40, hold 60 ± his roll - so a holding line rotates sooner) and a mate of his unit RIGHT BEHIND him
    (0.3 to `RotationBehindMetres` 1.5 m back along the line to his enemy, within `RotationFileMetres` 1 m of his file) who is `RotationFresherPercent`
    25 points fresher and not tired, not swinging / staggered / kicking, not walked by another system, not in a swap or its cooldown - the freshest
    such mate. THE LEVER (decided): the game's PUBLIC `Formation.SwitchUnitLocations(Agent, Agent)` (Formation.cs 2245 → the arrangement's swap -
    the game uses it for banner bearers and to keep front-unit types in front) for the SLOTS, plus an EIGHTH input wish (`AiInputState.Rhythm`) for
    the walk - the swap alone would send both men walking at once and open the line. THE STEPS: HOLD - the tired man stands (his movement 0, his
    melee attacks held, the guard raised) while the fresh man walks to `RotationBesideMetres` 0.7 m beside him at his depth (the side he stands on);
    BESIDE (no more than 0.4 m behind the tired man's depth, within 0.7 + 0.6 m sideways) - the slots swap and the tired man backs into the fresh man's
    start facing the enemy (the AI keeps facing its target; the vector is in his own frame) while the fresh man steps into the tired man's; DONE when
    the tired man is there; he then RESTS `RotationRestSeconds` 6 s (his movement toward the enemy clamped at his new place); neither swaps again for
    `RotationCooldownSeconds` 12 s. Not beside within `RotationApproachSeconds` 3 = called off (the tired man never left); a whole swap at most
    `RotationMaxSeconds` 6; the fresh man staggered on the way = called off. THE LINE NEVER OPENS: the front keeps its man at every moment (smoke:
    0 of 1440 file-frames empty). A held WALL man: his slot (what step 41 writes, stand firm pins and the pull walks him to) IS the swap's place -
    beside the tired man / the other's start - so the wall hold and the swap agree (`RhythmWallSlot`; the slots swap, nobody walks out); no slots to
    swap = no swap for a held man. (c) THE LULL (`LullEnabled`, player page): the UNIT TABLE (`RhythmUnits`, every 0.5 s from the same looks): each
    unit's centroid, its ENEMY UNIT (the one its men see nearest; a held wall sees none - its attackers' votes pair it), the direction to it, its FRONT
    (the 90th percentile of its men toward it) and its FRONT MEN (within `LullFrontDepthMetres` 1.5) and their mean bar. A PAIR (two units pointing at
    each other) in contact (`LullContactMen` 3+ men of both in contact) for `LullMinContactSeconds` 10 s (a contact lost under 2 s does not restart it)
    whose fronts are BOTH spent - below `LullStartChargePercent` 30 / `LullStartAdvancePercent` 40 / `LullStartHoldPercent` 50 by EACH unit's own
    order (later under a charge, sooner under a hold) - starts a LULL: each unit's LULL LINE = its front − `LullEaseMetres` 2 along its direction to
    the enemy; a man ahead of it (0.3 m slack) walks back to it facing the enemy, a man at it is clamped (his final movement toward the enemy taken
    out), the back ranks free to come up to it; a held wall's SLOTS shift back by the ease (stand firm walks its men there facing the enemy; the engine's
    frame pinned with step 68's call); the gap between the fronts opens ~3.5 m (smoke 1.0 → 4.4 m). DURING it (`LullRotates`) a tired front man swaps
    with a fresher man behind him with no enemy in reach. IT ENDS when either front regains `LullRecoverChargePercent` 10 / `LullRecoverAdvancePercent`
    15 / `LullRecoverHoldPercent` 20 points above its start bar (shorter under a charge), on a NEW charge order (the order kind turning into the charge
    group), a side BROKEN (its men looked at below half the start - a rout), the pair dissolved (one faces another unit), `LullMaxSeconds` 30, the
    switch off - then `LullCooldownSeconds` 20 s for that pair; step 79's waves end it through `EndLull(key, LullEnd.Wave)` (the seam). YOUR units take
    part only while `LullForPlayerUnits` (player page, on); you yourself are never moved. Never sprinting, never a back turned: the walk is a backpedal
    in his own frame, and the ease is never cumulative (each lull from where its front stands, at most the ease). (d) PRIORITIES (decided): a step
    back's backpedal > the wall's pull > the press's walk > THE RHYTHM'S WALK (a swap, a lull's walk back) > courage's walk back > the fear flock > the
    block hold; the clamps last - the press's, then the rhythm's (a lull's line, a rested man's place); so the press always wins (nobody walks past an
    enemy line), a swap or a lull overrides the fear band and the block hold for its seconds. Stand firm: the slots agree (the swap's places, the
    lull's shifted slot) - its walk back takes him exactly where the rhythm wants him. The brace: the tired man of a swap guards only (his melee
    attacks held, the guard up - the brace may already hold him). Courage: a man courage walks back is never picked (`OwnWish`); a surge or a punish
    step-in toward the enemy is CLAMPED at the lull line - the lull governs only MOVEMENT, attacks are never touched: punish's raised eagerness makes a
    man swing only when an enemy is in reach. The fever: a new charge order (which arms it) ends the lull; a fever sprint runs into the clamp.
    (e) 27 settings: `RotationEnabled`, `LullEnabled`, `LullForPlayerUnits` on the PLAYER page (27 settings), 24 Advanced in "Courage and cohesion
    (AI)"; new keys, no migration (FormatVersion stays 16). Gate: `ModEnabled` FIRST, `AthleticsEnabled`, then each part's switch; off = every swap
    ended and every wish let go at once, a lull ended at the next publish, the looks still measure (chances, spent pairs, would-start). (f) THE LOG:
    `[rhythm]` first-time lines (mission start, the first chance OFF, the first swap asked / beside / done / not finished, the first refusal, the first
    LULL, the first man walked back, the first wall slot shifted, the first swap during a lull, the first lull ended) and two [summary] "battle rhythm"
    lines (rotation: tired front men looked at ON | OFF, chances, swaps asked / done / slots swapped / ended by / refused by reason, the bar of tired vs
    fresh, the hold's wait, men rotated and during lulls, the bar of men IN CONTACT ON vs OFF, input frames; lulls: started, front bars at the start,
    duration, the gap opened, ends by reason, men walked back, wall slot shifts, both fronts spent ON vs OFF, would-start OFF, refused for your units,
    cost). (g) UNVERIFIED in game: that the engine walks a man by the swap's vector in a tight press (the AI's collision avoidance), that the slot swap
    makes the wall's men change places cleanly, that a lull's gap holds against the native charge AI (the clamp), the front bars' thresholds against
    real battles - PLAYTEST D43.
73. **The lull that shows, no wrapping round the ends** (step 78b, Toni's 225 v 229 battle 2026-10-22 16:08-16:17, battle_terrain_b, 435 s:
    "between the two fronts [a lull], never, not for a sec"; the enemy's 229 recruits - D 5.08, 46 files, 69 m - far wider than his ~31 m line, its
    wings round both his ends: "they should not appear ... like packs of dogs, barking at each other after the first time they try each other ... they
    don't want to do funny stuff like going around the other group, flanking, they want to stay in their pack"). THE EVIDENCE: 2 lulls in 435 s;
    "both fronts SPENT in contact" on only 27 unit looks - the bar of the men in contact averaged 29% but the rotation (379 swaps) kept the FRONT men's
    bars up, so step 78's test (the front men's bar) rarely held; at the first lull the fronts were MESHED ("the gap between the fronts -20.7 m") and
    easing each 90th-percentile front back 2 m opened nothing visible; one of the two ended at once by a new charge order; step 77's wrap allowed
    "round the end with his mass" on 4763 looks. (a) THE BREATHER CLOCK (decided): a pair in CONTINUOUS contact (lost under 2 s does not restart it)
    for a time rolled once per spell (`RhythmUnits.Dice`) between `LullClock*MinSeconds` and `LullClock*MaxSeconds` of the more aggressive of the two
    units' order groups (charge 30-60 s, advance 25-50, hold 20-40 - later under a charge, sooner under a hold) lulls once BOTH UNITS are worn -
    `LullClockChargePercent` 55 / advance 60 / hold 65 - or the pair's BLOWS in contact have slowed to `LullAttackRateFallenPercent` 60% of their
    peak in the spell (every AI melee release of a man in contact at his last look ÷ the men in contact, a minute, smoothed over ~6 s; the peak after
    4 s of contact). A UNIT's bar = the mean bar of its men within `LullUnitDepthMetres` 8 of its front (the 90th percentile toward the enemy) - the
    rotated fresh men and the tired men behind them alike, so the rotation never hides tiredness. Step 78's thresholds (`LullStart*Percent` 30 / 40
    / 50) are the SPENT trigger now, on the same unit bar: both below after `LullMinContactSeconds` 10 = a lull at once. After a lull the clock starts
    again with the next contact (a new roll). The recovery reads the unit bar too; `LullRecover*Percent` 10 / 15 / 20 → 20 / 25 / 30 in defaults.json
    (a breather long enough to see - config FORMAT 17 moves a file's untouched old values once, a hand-set one kept and said). A NEW charge order in
    a lull's first `LullChargeGraceSeconds` 3 s is deferred - it ends the lull once they have passed, if the unit is still on a charge. Expected: a
    6-minute grind between two lines sees ~4-7 breathers (the smoke's meshed grind: 7, two by the clock), each ~10-20 s. (b) THE CLEAR GAP (decided):
    measured from the MEN, never a percentile front - every 0.5 s the pair's men in `RhythmMath.GapBinMetres` 2 m bins along the contact (64 bins),
    in each bin with men of both A's front-most and B's rear-most toward A; the gap there = their distance (negative = meshed); the contact PLANE = the
    median of the bins' midpoints, each side's FRONT = the median of its nearest men. The LINES at the start: A's = min(the plane − `LullGapMetres` ÷ 2,
    A's front − `LullEaseMetres`), B's mirrored - a meshed fight separates about its middle, a fight with a gap still backs off the ease. Men ahead of
    their side's line walk back facing the enemy (step 78's backpedal - a man mixed into the enemy walks straight back through them, the engine's
    avoidance steering him round), the rest are clamped at it, held walls' slots shift back by the line's distance from their nearest men. The gap
    HOLDS when the bins' 25th percentile reaches `LullGapMetres` − 0.5 m (the time it took logged); while it does not (after 3 s) a side standing AT
    its line (its nearest men within 0.6 m) steps it back 0.25 m a publish - at most `LullMaxBackMetres` 8 beyond where the lull first put it - a
    side still ahead of its line keeps it. The axis is one for the pair (B's = A's turned round). (c) NO WRAPPING (decided): step 77's wrap only
    while `PressWrapAllowed` (off - a new key, no migration). Past a line's end + `PressEndSlackMetres` the front a man may not cross BENDS back
    toward his side by `PressWingBendDegrees` 30 - p metres past the end it stands tan 30° x (p − 1) in front of the line's front: an overlapping
    wing is clamped along it (the clamp's normal cos 30° into the line + sin 30° outward, within 0.6 m of it), slides toward the end and stands off
    it facing it (a refused flank) - never level with the line's flank or behind it; a wing man forward of it is walked straight back to it (a Push
    to the bent front); a man behind the line walks round its nearest end to the bent front there; a wing man of a unit in contact is side-clamped
    past the end + the slack like a rear-rank man. The smoke: Toni's 69 m on 31 m under a stand-in charge that sends every man beyond the ends round
    them - step 77's wrap 80 men past the front, step 78b 0, every wing man at an end. (d) ORDERS STILL WORK (decided): the rule is about the AI men's
    own movement - a unit ORDERED to a place (its movement order a Move - yours or the AI general's flanking) is free beside or behind the line
    (`PressAction.Ordered`); a charge / advance / hold is the AI's own pressing. (e) 16 settings (473 → 489): `PressWrapAllowed`, `PressWingBendDegrees`
    after `PressEndSlackMetres`; 14 `Lull*` after `LullRotates` (Advanced, "Courage and cohesion (AI)"). (f) THE LOG: `[rhythm]` first LULL (its
    trigger, the unit bars over their men vs the front men's, the blows vs the peak, the gap from the men, each line's distance), first lull GAP
    OPENED, first LINE STEPPED BACK, first CHARGE ORDER deferred, first lull ENDED (its trigger, held after … s); a THIRD [summary] "battle rhythm
    (step 78b) - THE LULL'S TRIGGER AND GAP" line (by trigger, the trigger looks, the clock waiting, would-start OFF by trigger, the gap mean / p10 /
    p90 / lowest, the time to open, never opened, lines stepped back, charges deferred); `[press]` first WING man held at an end / walked back, first
    man ORDERED beside a line; a third [summary] "press, don't pass (step 78b) - NO WRAPPING" line (wings held ON | OFF, men, the farthest, walked
    back; crossings round an end; the old wrap counter - 0 with the switch off; ordered looks). (g) UNVERIFIED in game (PLAYTEST D43b): that the
    native charge AI stays behind a lull line long enough for the gap to show; that a meshed crowd backs out (men walking back through the enemy);
    that the wings stand at the bent front rather than jostle; the clock's and the bars' numbers against real grinds.


74. **Each man rests on his own - the lull removed** (step 78c, Toni 2026-10-22 after playing 78b: "this doesnt work at all, dont add this dance move,
    it looks crazy, they are at it the whole time, then they like for some reason all just turn their backs and walk away very far off, then they
    are at it again ... only per men rule, if tired - step back and wait to recover, so that if all are tired they should naturally keep distance
    ... if guy next to you rushes, there is a chance to make you go in too"). SUPERSEDES decision 72 (c) (the lull), 72's lull parts of (d)-(e) and
    decision 73's 78b.1 / 78b.2 (the breather clock, the unit bar, the clear gap's lines, the line stepping back, the charge grace): the code, its
    29 settings, tests and smoke are gone. KEPT: 72's rotation (it never depended on the lull - `LullRotates` only let it run without an enemy in
    reach), 73's wrap-off and wing bend (78b.3), step 77's press. (a) WHO: every AI man on foot courage looks at and every held wall man (the
    rotation's look); never you, riders, no formation; needs the Athletics system. (b) GOING TO REST: at the fight (an enemy within
    `RestReachMetres` 3 from courage's scan, or pressing an enemy front - step 77's clamp on him) with his bar below his own TIRED mark - rolled once
    per rest from `RestTiredMinPercent` 20 to `RestTiredMaxPercent` 45 - and a READY mark he can reach (rolled at the start from his unit's order:
    charge 55-95, advance 60-95, hold 65-95 - eager men go back sooner, a holding line waits longest; capped 2 points under his wound cap; no rest
    when that leaves under 5 points above the tired mark). (c) RESTING: every 0.1 s, from his nearest enemy at the last look, he WALKS back
    (`RestMath.WalkBack`: out of reach + 0.3 m, at most `RestMaxBackMetres` 4 from where the rest began - the stick at most 0.6, never a run; the
    input in his own frame while his AI keeps facing its enemy - a backpedal), then is HELD there - the part of his movement toward the enemy taken
    out (step 78's clamp, the press's way), so the game's charge AI never drags him in; his attacks held and his guard up unless an enemy is within
    `RestDefendMetres` 2 (then he fights as usual). (d) GOING BACK IN: at his ready mark (his own) - the standard AI has him again; or CONTAGION, the
    only social rule: each time a man of his unit within `RestContagionMetres` 3 goes back in, a resting man rolls `RestContagionMaxPercent` 60% ×
    (bar − floor) ÷ (ready − floor), floor = his tired mark + `RestContagionFloorPoints` 10 (0 below it); those who go may take their own
    neighbours (a cascade through one reused queue - still each man's own roll). No unit-wide event, no wave, no shared clock. (e) WHAT FOLLOWS BY
    ITSELF: both fronts spent = both step back, a gap opens without any unit rule; one side spent = the fresh side follows and presses (the
    standard AI decides). (f) PRIORITIES (decided): the rest's walk sits right after the wall's pull and BEFORE the press's walk, the swap's, courage's
    and the block's (Toni: it wins over them) - but a step back's backpedal, the wall's pull and the fear flock's walk win over it (fear and the rout
    still win: a routed / excluded man's rest ends at once); the rest's clamp is step 78's (after the press's clamp). THE ROTATION FIRST: a tired
    front man with a fresher mate right behind swaps (no rest needed - `RestIn.Rotated`), else he rests. THE STEP BACK (step 5d) stays: a different
    thing (2 m for 1.5 s after a swing), both move him away from the enemy, the backpedal wins while it runs. (g) A SHIELD WALL (Toni, the same day:
    "a man in a shield wall must never leave it to rest" - every wall state: a held wall, step 68's advance as one, stand firm): a held man rests IN
    his slot - no walk, no clamp (the wall pull must still walk him back to his slot), step 42's leash 0 for him (never a step out of the slot), his
    attacks held, guard up; the rotation may still swap him (that is the wall's own move); the wall moves only as one. (h) THE PLAYER: never
    touched; his orders work; a NEW charge / advance order does not force resting men in (they go at their mark or by contagion). (i) MEASURED:
    the [summary] "rest (step 78c)" lines - rests started (in a wall slot), lasted, back in by own mark / contagion / other, stepped back from the
    start mean / p90 / max, the farthest behind his own front (rests more than 5 m behind it), men TURNED AWAY while resting (the look direction
    against the nearest enemy, should be 0), WALL men resting in their slots more than 0.5 m out (should be 0), the marks rolled; and per side the
    NATURAL GAP of 2 m+ (`RestGapDiagnosticMetres`; the 25th percentile of step 78b's 2 m bins between two engaged fronts) - a diagnostic only.
    (j) SETTINGS: 489 → 476 - the 29 lull keys out (no format change: the reader ignores unknown keys and `ConfigFile.DropRetiredLullKeys` drops
    them from any file, one note each - FormatVersion stays 17), `RestEnabled` on the PLAYER page (27 → 26) and 15 `Rest*` Advanced after
    `RotationRestSeconds`. (k) UNVERIFIED in game (PLAYTEST D43c): that the engine walks a man backwards facing his target (the backpedal's way),
    that the clamp holds against the native charge, the numbers against real grinds (how long rests last, how often a gap shows).
75. **War cries only** (step 79, Toni 2026-10-22: battles should sound alive). SOUND ONLY - a cry never changes movement, readiness, the rest marks,
    morale or orders (the smoke runs step 78c's grind with the cries on and off: the rests are identical). (a) THE VOICE: `Agent.MakeVoice(VoiceType.Yell,
    NoPrediction)` on foot, `HorseRally` on a horse - exactly what vanilla's own yell plays (`Agent.OnTickParallel`'s `_wantsToYell`). HOW VANILLA CHEERS:
    when the PLAYER's `OrderController` issues a charge (F1 F3 - `PlayOrderGestures`), the ordering hero says "Charge!" (`VoiceType.Charge`), the
    formation's median man plays the `event:/alerts/nods/attack` sound, and at most once per 30 s per controller every unit of the formation runs
    `Agent.YellingBehaviour()` - an AI man yells with chance morale x 0.33 % (morale 70 → 23 %) after 1.5-2.5 s (`YellAfterDelay`, native). The AI
    general's charges (its tactics set `MovementOrderCharge` directly) are SILENT in vanilla. Ours adds to vanilla's yell for your orders and gives the
    AI's charges a voice. (b) CAUSES: a rested man going back in by his own ready mark (78c's `GoIn` - not a swap, not by contagion) rolls
    `WarCryBackInPercent` 35; a mate going in with him by contagion rolls `WarCryAnswerPercent` 20 and answers 0-`WarCryAnswerDelaySeconds` 1 s later; a
    formation whose order poll kind became a CHARGE (charge / charge a target / attack an entity / the AI's tactical charge) from anything that was not
    one - yours or the AI general's - roars: each eligible man rolls `WarCryChargeSharePercent` 40, at most `WarCryChargeMaxMen` 12 a unit, each cry
    0-`WarCryChargeStaggerSeconds` 1.5 s after (uniform), one roar a unit per `WarCryChargeUnitCooldownSeconds` 15 s (an AI charge flickering on and off
    roars once); the pass over the men starts at a random man so the cap does not always pick the same ones. (c) LIMITS, checked when a cry is DUE:
    one cry a man per `WarCryManCooldownSeconds` 8 (and one waiting at a time), `WarCryUnitPerSecond` 8 a unit and `WarCryBattlePerSecond` 16 a battle in
    any 1 s (sliding windows over the cries played, Core `WarCryLimiter`); a cry over a limit is DROPPED (counted), never delayed. DECIDED: a charge by
    many units at once shares the battle's budget - each roaring unit queues at most ceil(16 x max(1, 1.5) / units) shouters (`ChargeShoutersPerUnit`):
    without it ten units queued 120 cries, the first 16 played in 0.2 s and the rest were dropped (a burst, not a staggered roar). (d) WHO: never the
    player, never a man running away / retreating (routed, fleeing), never the dead; riders on a charge; the rest's causes are men on foot anyway.
    (e) WHEN: queued from the rest's look and the order poll, played from the logic's tick (`TickWarCries`, after the poll) - never in an engine callback;
    off (or the master switch) = every waiting cry dropped at once. (f) MEASURED: the [warcry] first-time lines (the first cry of each cause, the first
    drop by each limit, the first charge roar) and the [summary] "war cries (step 79)" line - cries by cause (riders), rolls / queued / not eligible,
    DROPPED by each limit, the charge orders (roared / cooling / past the shouter cap), the PEAK cries in any 1 s (the battle's, one unit's) and one
    man's shortest gap. (g) SETTINGS: `WarCriesEnabled` on the PLAYER page (26 → 27) after `RestEnabled`, 10 `WarCry*` Advanced in "Courage and cohesion
    (AI)" after `RestGapDiagnosticMetres` (476 → 487), no config-format change (a missing key takes its default). (h) UNVERIFIED in game (PLAYTEST D44):
    how it sounds - whether Yell / HorseRally carry, whether 16 a second is a roar or too much, whether the engine itself limits voices.
76. **AI kicks and bashes land on enemies, not friends** (step 80, Toni's logs since step 68 - "224 of 275 kick hits landed on a friend"; his
    2026-10-22 battles: 16:17 - 668 landed on a man, 427 on a FRIEND (64%), 176 on the target, 64 on another enemy; 17:18 - 570 landed, 314 on a
    friend (55%), 193 / 63 on enemies; step 68's line check said "held 159, clear 818" there - it passed almost every kick). THE CAUSE (the code):
    step 68 tested a friend against the LINE FROM HIM TO HIS TARGET (within 0.5 m of it), but a kick / bash goes along HIS FACING and hits the first
    body there - the target may stand up to 40° (`AiKickFacingDegrees`) off it, 1 m beside the foot's path at 1.5 m, and a rear-rank man's front mate
    stands straight ahead of him while his target stands half a file to the side; and 0.5 m to a body's CENTRE is closer than a body (0.4 m round)
    lets a foot pass. (a) THE STRIKE PATH (Core `AiKickPathProbe`, allocation-free, fed one friend at a time from the game's own
    `Mission.GetNearbyAllyAgents` into a reused list): in his own frame (`Agent.GetMovementDirection` - the facing step 62's angle uses) a strip from his
    shoulders `AiKickReachMetres` 1.6 long and a half-width either side - a kick's `AiKickFriendClearMetres` (the key kept; now the KICK's half-width;
    0.5 → **0.6** - a foot's 0.12 + a body's 0.4 + margin), a bash's `AiBashPathHalfWidthMetres` **0.75** (a shield's 0.3 + a body's 0.4 + margin). THE
    ENEMY MUST BE IN IT (0 < along ≤ reach, ≤ the half-width off the line - the cheap gate on the target's distance and angle, `AiKickMath.TargetInPath`,
    before any read), and NO FRIEND IN IT CLOSER (along > 0 - step 68's 0.2 m let a neighbour half a step forward through, whose body reaches a bash's
    sweep -, along < the enemy's, ≤ the half-width; his side's horses count). Verdicts: CLEAR → kick; the enemy OFF THE PATH → the decision waits like one
    not in front (`AiKickSkip.TargetOffPath`); A FRIEND FIRST → waits for a clear path (`FriendInTheWay`); a friend first within `AiKickRearRankMetres`
    **1.2** of him = a REAR-RANK man → refused with NO wait (`BehindFriend` - he does not re-read the engine every look for 3 s). (b) THE PRESS: a bash's
    0.4 s guard gives the men time to move - when it ends, the target and the path are read again just before the key; not clear = the attempt ends with
    no key (`AiKickEnd.PathBlocked`, its cooldown stands). A kick's key comes at the decision, so its decision IS the press. (c) MEASURED: the [summary]
    "ai kicks - whom they hit and the strike path (step 80)" line - each PLAYED kick and bash by whom it hit first (his target / another enemy / an
    enemy's shield / a friend's body / a friend's shield / a horse or untracked / MISSED), the friends' share of the hits, the enemy hits a man-minute in
    contact (the effective rate), the path's verdicts at the look by kind and at a bash's press, the probes; the first REFUSED / ENDED lines. (d)
    CALLS (Claude): the rates are NOT raised (Toni) - fewer kicks start; the rear rank gets no wait; the path is the man's facing, not his body's line to
    the target; widths from bodies, not from the decompile (the kick's real collision is native - `Agent.KickClear` is an engine call with no managed
    geometry). (e) SMOKE (a stand-in engine with its own reach / widths: a foot 1.25 + 0.4 m, sides 0.52 / 0.7 m): two two-deep lines of 12 files, half
    shieldmen, shuffling and turning, 3 minutes - ON friends 4.2% of the hits (7 of 166), enemy hits 1.12 a man-minute, asks 1.35; OFF 27.2% (85 of 312),
    1.62, 2.25. (f) SETTINGS: 2 Advanced in the Impact group after `AiKickFriendClearMetres` (487 → 489), live; config format 18 moves a format 14-17
    file's untouched 0.5 once (a hand-set value kept and said). (g) UNVERIFIED in game (PLAYTEST D45): the friend share in Toni's next log, how many fewer
    kicks / bashes start, whether 0.6 / 0.75 match the engine's real kick and bash.
77. **AI kicks and bashes as a share of the melee moves** (step 81, Toni 2026-10-22: "since they add good disturbance to the battle and also dont do
    much damage I want to try increase them: shielded men kick 20% of the time, shield bash 35% and swing the rest. If they have a spear prefer holding
    ground with it like now, and if the enemy comes close then use the other skills. Unshielded - 35% kick and the rest swing, this will make shockers
    throw people around more. Also make the min swing stack from 2 to 3 ... make sure kicks and bashes use it. Then make all those moves take 8
    athletics each". His 17:18 log: shieldmen 5407 swings / 458 bashes, unshielded 5554 swings, 318 kicks in all; the brakes were the bash credit's cap
    of 2 (2545 earned bashes lost), the bar gate (269750 refused looks), the dice, the 2 s bash cooldown). (a) THE DECISION POINT = the AI's own decision
    to attack - his melee WIND-UP (the engine's `ReadyMelee` on channel 1), which step 13's phases already see on the action poll: `NoteAiMeleeReady`
    queues it (the phases may run inside a hit callback - no engine read there), the logic's tick rolls it the same frame (`DecideAiMove`). Chosen over
    (1) the input hook's rising attack edge (the hook only runs for men we have hooked; hooking every man in contact for every frame to catch an edge
    costs a callback per man per frame) and (2) keeping the look's per-minute die (a rate cannot "take the place of a swing" - Toni's shares are of his
    MOVES). (b) THE ROLL (`AiKickMath.Roll`): the enemy he fights (his target, else his immediate enemy) within `AiKickReachMetres` 1.6 and
    `AiKickFacingDegrees` 40 (`AiKickMath.InReach`) - a shieldman (a shield in hand, `IAiKickBody.Hands`) kick `AiShieldmanKickPercent` **20** / bash
    `AiShieldmanBashPercent` **35** / swing the rest; no shield kick `AiNoShieldKickPercent` **35** / swing the rest; the shieldman's kick + bash never over
    100 (the bash cut to 100 − kick, `ShieldSharesCut`, said in the describe line). A rolled bash into a RAISED shield facing him is a kick
    (`AiKickKicksARaisedShield` kept - folded into the roll). Beyond kick reach nothing is rolled: a spearman keeps his ground and thrusts (step 76's
    preference untouched), the shares start once the enemy is close. (c) TAKES THE PLACE OF THE SWING: the attempt's first phase is the guard for BOTH
    kinds now (`AiKickMath.FirstPhase`) - his attack bits out WITH a guard = step 16's ready cancel (out without one would RELEASE the blow); a kick's lead
    `AiKickMath.KickLeadSeconds` 0.15 s (plumbing), a bash's 0.4 s; then the kick key (a kick with the guard down). A swing rolled = nothing touched. (d)
    THE REFUSALS (he swings instead), by group: SAFETY - staggered / down / his kick still playing / fewer points than the cost / the pool full; STACK
    EMPTY - `KicksUseSwings`: no stored swing (a man the stack holds, or his stack's count 0); COOLDOWN - within `AiKickCooldownSeconds` (now THE one
    minimum between two kicks OR bashes, 8 → **2** s; step 66's `AiBashCooldownSeconds` retired); PATH - step 80's strike path (no waiting any more: a
    friend first / the enemy off the path = he swings). Riders, routing men, a ranged weapon, no target, out of reach / not in front = not a move in reach
    (counted apart). (e) RETIRED as frequency controls (14 keys - `ConfigFile.DropRetiredKickKeys` drops them from any config.json with one note each, 78c's
    pattern): `AiKicksPerMinute` and its factors (`AiKickTurtleMultiplier` / `Seconds` / `Blocks`, `AiKickFeverMultiplier`, `AiKickSkillLowPercent` /
    `HighPercent`), the bar gate (`AiKickMinBarPercent` / `FullBarPercent` - none below 35%), the wait for reach (`AiKickWaitSeconds` / `Metres`), the bash
    credit (`AiBashShareOfSwingsPercent`, `AiBashDelaySeconds`, its cap of 2) and `AiBashCooldownSeconds`. The brakes now: the swing stack
    (`SwingStackMinimum` 2 → **3**; a kick / bash spends one and cannot start at 0 - unchanged) and the price (`CostPerKickOrBash` 6 → **8** × the hero /
    leader multipliers: 8 / 6 / 4.5; a swing stays 10). (f) MEASURED: the [summary] "ai kicks - shares of the melee moves in kick reach (step 81)" line -
    per man type the moves, the shares ROLLED and DONE against the targets, the refusals by group; kicks and bashes asked a man-minute in contact; a raised
    shield kicked; the refusals by reason; the Athletics spent. The "ai kicks" lines' reason columns are the man types now (shieldmen / no shield); the
    "why not at a look" clause = "at the decision point" (wind-ups, not a move in reach). (g) SMOKE (step 80's two two-deep lines driven by the men's own
    swings, the stack on, 3 minutes): fresh - shieldmen rolled 21.0 / 33.4 / 45.6 % → done 17.3 / 20.8 / 61.9 % (the path 134 - rear-rank bashes, the
    cooldown 17), no shield 34.0 / 66.0 → 28.1 / 71.9 % (the path 56), 4.28 asked a man-minute in contact, the stack never refused (it holds the man - all
    his moves - at 0, never the share); tiring (no refill) - done 4.3 / 5.3 / 90.4 % and 7.8 / 92.2 % (no Athletics for it 371); 0 kicks / bashes at an
    empty stack; 1000 men winding up ~8 swings a tick 0.004 ms a tick, 0 bytes. (h) CONFIG FORMAT 19: `SwingStackMinimum` 2 → 3 (any older file),
    `CostPerKickOrBash` 6 → 8 (a format 15-18 file), `AiKickCooldownSeconds` 8 → 2 (a format 13-18 file) - untouched values moved, hand-set ones kept and
    said once. (i) UNVERIFIED in game (PLAYTEST D46): the guard-then-kick plays as a KICK (not a bash) and is not dropped (`played as the other kind`, `NOT
    played`); the cancelled wind-up looks natural; the measured shares in Toni's next log.
78. **Meeting a bash or a kick the right way** (step 82, Toni 2026-10-22: "a valid way to transfer your body's kinetic energy ... if met properly the
    initiator should be caught off balance"; then "more about staggers and men thrown off balance" - the ceilings raised). A shield bash comes from the
    basher's LEFT hand to the defender's RIGHT side - a RIGHT guard is the correct meet; a kick's is a DOWN guard. (a) THE SPIKE (AI_NOTES "Step 82"): the
    engine stops a bash with ANY raised shield (`AttackBlockedWithShield`, Blocked - not registered, no knock-back) and LANDS every kick and every bash into a
    weapon guard (Toni's logs: kicks into a shield 0, "other" 0 in 8 battles); no collision field tells a guard direction for an alternative attack, so the
    guard is read OURSELVES at contact (channel 1's action - a weapon guard's action names its side, Forward = down -, its direction, the engine's defend
    flag; Core `ImpactMeetMath.GuardOf`), in the same decision (the decorator's seams for a landed one, OnMeleeHit for a stopped one). (b) THE ROWS (the
    pressure, the S-curve, tiredness, skill, toughness, direction and the halving unchanged): a CORRECT meet - the defender takes nothing (a landed one held:
    the knock-back refused, the reaction cut), the ATTACKER rolls fall then stagger on the riposte's pressure x_r (his tiredness, the defender's body / guard
    / skill against his blow): bash on a shield right **70 / 30**, a weapon right **45 / 15**, kick on a shield down **60 / 25**, a weapon down **50 / 30**
    (stagger / fall); a bash the shield STOPPED with a wrong guard - the blocker's rows on x in full (a block's), **85 / 40 / drop 35**; a bash that LANDED -
    weapon wrong **95 / 50**, no guard or mid-swing **98 / 60 / drop 45** - the knock-back gets through at the ceiling × step 65's brace curve (held = braced,
    no fall / drop) - Claude's call: the bare S(x) from 0 would never knock back a fresh unguarded man; a kick met any other way = step 65 exactly. Made by
    the levers already proven: the riposte's forced stagger and step 62's own blow on the attacker, step 51's stagger / blow / drop on a stopped bash's
    blocker, step 50 / 65's live kick on a landed one. (c) THE AI READS IT: an AI man targeted by a starting kick / bash (an AI attempt at its start; any
    other - yours - when the poll sees it) rolls once at `MeetAiReadScalePercent` × the chance he blocks a swing correctly (the game's
    `AIBlockOnDecideAbility` × the right-direction share of `AiRandomizedDefendDirectionChance`, step 30's tired guess inside: a legionary ~25 %, a lord
    90 % falling to 25 % empty); read = the correct guard (DefendRight / DefendDown) through step 16's input hook from `MeetAiReadReactionSeconds` 0.15 s
    after the start until the hit (`MeetAiReadMaxHoldSeconds` 1.5 at most); a hit sooner = too late. The player is judged the same way, never steered. (d)
    STEP 81's RAISED-SHIELD SWAP kept only where right: the AI basher kicks a raised shield guarding RIGHT (the bash would be met) and keeps the bash on one
    guarding any other way (the AI's own raised guard is DOWN - the blocker is at risk); a kick into a DOWN guard is now the kicker's risk, so the old blanket
    swap is gone. (e) SETTINGS: `ImpactMeetRulesEnabled` (the player page, 28) + 19 Advanced in the Impact group after `AiKickRearRankMetres` (the 16
    ceilings, the read's scale / reaction / hold), all live; no config-format change. (f) MEASURED: the [summary] "impact meets" lines (each meeting by row,
    outcomes on the attacker vs the defender, the guard read and the engine's own verdict on stopped ones, the AI's reads by bar band, too late). SMOKE:
    an average shieldman at half his bar bashing a skilled shieldman 400 times - FRESH: read 59 %, the basher staggered 24 % / down 6 %, the defender never;
    TIRED (10 %): read 28 %, the defender staggered 35 % / down 30 % / shield 24 %; 1000 men 0.037 ms a tick, 0 bytes. (g) UNVERIFIED in game (PLAYTEST D47):
    the shield guard's direction read at contact, DefendRight honoured in AI input, how it looks, the read's timing.
Decisions 7–10 and the new parameters `DamageRandomOnShields`, `ExhaustedRecoverPercent`
(retired in 5c), `TargetBarMaxDistance` and `TargetBarLingerSeconds` (LATER, step 7), `FormationBarsAlways` (LATER, step 8),
`HudRefreshSeconds`, `FormationStatsRefreshSeconds` came out of step 2's research
(`docs/RESEARCH.md`, "Design implications"), settled by Claude while Anton was away
(2026-09-27) — every one is a default he can overturn.

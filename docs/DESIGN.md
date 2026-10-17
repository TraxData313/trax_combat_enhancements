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
- **What is a blow**: a melee swing or thrust, a shot, a throw. A couched lance or braced
  spear hit has no swing, so it costs one blow when it LANDS. Siege engines (ballista, onager)
  cost nothing.
- **Kicks and shield bashes cost too — less** (Anton, 2026-09-28; built in step 18): each costs
  `CostPerKickOrBash` (3) POINTS × the same hero and party-leader multipliers as a blow — 3 for a
  soldier, 2.25 for a hero, about 1.7 (1.69) for a party leader; 0 = free, as they were until step 18.
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
  free. The AI's and yours alike, on foot or mounted. Charged ONCE per blocked blow (a swing touching the
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

- **MCM, group "Defaults"**: **Revert all to defaults** puts every setting back to its
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
*Advanced*, which stays last. One vocabulary: the pool, the bar and its points are *Athletics*; the
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

| Key | Default (initial) | What it does |
|---|---|---|
| `ModEnabled` | true | THE master switch. Off = the whole mod steps aside and every battle is pure vanilla (live, even mid-battle); the log still records each battle for comparison. |
| `DamageRandomEnabled` | true | Master switch for damage randomness. |
| `DamageRandomPercent` | 50 | ± spread in percent. 50 → a 50-damage hit lands for 25–75. 0 = off. |
| `DamageRandomMelee` | true | Randomize melee hits. |
| `DamageRandomRanged` | true | Randomize arrows, bolts and thrown weapons. |
| `DamageRandomOnMounts` | true | Randomize hits that land on horses too. |
| `DamageRandomOnShields` | false | Randomize the damage a shield takes when it blocks. |
| `DamageReductionPercent` | 25 | Step 25 (Anton): every hit on a person, a horse or a shield deals this % less, AFTER the roll (an average 100 hit lands 75) - melee, ranged, kicks, bashes, charges, whether the roll is on or off; falls and objects never. A positive hit never below 1. 0 = the game's damage; range 0-90. |
| `AthleticsEnabled` | true | Switch for Athletics (the stamina bar). |
| `AthleticsPoolFloor` | 50 | Smallest possible Athletics pool (for anyone with a low Athletics skill; 0 = none, never below 1 point). |
| `AthleticsPoolPerSkill` | 1.0 | Pool per point of Athletics skill (1.0 → the bar tops at the skill). |
| `AthleticsPeakPercent` | 75 | At or above this % of their own pool a fighter is at full strength (green); below it they weaken in a straight line to 0. |
| `HealthCapsAthletics` | true | Health left caps the usable pool. |
| `CostPerBlow` | 10 | Athletics points one blow costs before multipliers (points, whatever the pool). defaults.json 3 since step 43 (Anton: the AI's eagerness now follows its bar; config format 8 moves a file's old 10 once), 7 since step 46 (Anton's playtest; config format 9 moves a format-8 file's 3 once). |
| `CostPerKickOrBash` | 3 | Step 18 (Anton): Athletics points one kick or shield bash costs before the hero and party-leader multipliers (3 / 2.25 / 1.7), once, when it starts, landed or not; never starts the attack pause. 0 = free (steps 5-17). |
| `CostPerShieldBlock` | 1 | Step 22 (Anton): Athletics points the DEFENDER pays for a melee blow he blocks with his shield on the correct side, before the hero and party-leader multipliers (1 / 0.75 / 0.56); once per blocked blow; does not restart the refill delay while `OnlyWrongSideBlocksStopRefill` is on (step 29); never an attack pause or a step back. 0 = free (steps 5-21). |
| `CostPerWrongSideShieldBlock` | 5 | Step 22 (Anton): the same for a blow his shield stops on the WRONG side (the engine's `CorrectSideShieldBlock` false) - 5 / 3.75 / 2.81. 0 = free. defaults.json 2 since step 43 (Anton; config format 8 moves a file's old 5 once). |
| `CostPerWeaponParry` | 2 | Step 22 (Anton): the same for a blow he blocks or parries with a weapon, no shield (a chamber block too) - 2 / 1.5 / 1.13. 0 = free. |
| `OnlyWrongSideBlocksStopRefill` | true | Step 29 (Anton): true = only a WRONG-side shield block restarts the refill delay (and ends a refill run); a right-side shield block and a weapon parry still cost but the refill keeps running underneath. false = every paid block restarts it (step 22). Attacks, kicks and bashes restart it either way. Live. |
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
| `SwingStackMinimum` | 2 | Step 32: the stack never holds fewer than this - even at empty. Range 1-10. |
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
| `ShieldWallHoldsSlots` | true | Step 41 (Anton, 2026-10-17): every AI man on foot in a shield wall, square or circle keeps his slot instead of walking out of it at the enemy he fights (the engine's "keep with the formation" flag, rewritten right after vanilla's own update - §2 "The shield wall holds its slots"). Any order but a charge or a retreat; not detached, fleeing, or far from his slot AND behind the line; both sides; never you, riders, a siege's attackers, naval battles. Live: off = vanilla's own value written back at once. |
| `ShieldWallStepOutMetres` | 1.0 | Step 42 (Anton, 2026-10-17, his playtest of 41: held men reached and stopped, rarely hitting an enemy who stepped back): the LEASH - a held man may step up to this many metres out of his slot at an enemy close in front of it (within this + 2 m of the slot), then is held again and walks back; he may step again once back within 0.3 m (at most half the leash) of his slot (§2 "The shield wall holds its slots"). 0 = step 41's hard hold exactly. 0-3. Live. |
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
| `AiAggressionFreshPercent` | 60 | Step 43 (Anton, 2026-10-17: "a man at work doesn't hold a pace until he drops; the more tired he is, the less he swings"): AGGRESSION BY ATHLETICS - every AI man on foot's two melee attack-decision values, `AIAttackOnDecideChance` (the chance to start an attack at a combat decision: the game's 0.144 charge / 0.096 other orders / 0.05 shield wall, square, circle) and `AIAttackOnParryChance` (the riposte, 0.08 − 0.02 × defensiveness), × a share that is this % at a FULL bar and slides in a straight line to `AiAggressionExhaustedPercent` at an empty one; the bar = points ÷ his FULL pool (the brace's reading; a wound's cap is in it - no special case). Written by the stat decorator after the base model (no floor of ours; × step 5e's m too when `AttackRateAiDecisions` is on), in 0.05 steps of the share. Never the player, never riders, never ranged (`AiShootFreq`, the aim). Tournaments and duels too. 100 / 100 = the game's own AI. Range 0-200. Live. |
| `AiAggressionExhaustedPercent` | 20 | Step 43: the share at an EMPTY bar (0.144 → 0.029 on a charge). On top of the pause after each attack (unchanged) - a tired man has two brakes. Range 0-200. Live. |
| `FeverEnabled` | true | Step 43: the FEVER - a NEW charge / advance order on an AI formation (step 33's order poll and kinds: charge, charge a target, attack a gate, the AI's tactical charge; advance, the AI's advance) arms each of its AI men on foot with a store of his current points ÷ `FeverPointsPerSecond` seconds; while armed or burning his share is lifted to `FeverAggressionPercent`, fading back to his tired share (§2 "Aggression and the fever"). A hold / move / retreat / stop / follow order ends it, armed or burning. Never the player, never riders, no formation = none. Off = the bar's share only, fevers end at once. Live. |
| `FeverPointsPerSecond` | 10.0 | Step 43: the store = his CURRENT Athletics points ÷ this, seconds (100 points = 10 s, 80 = 8 s, a tired 40 = 4 s), read at the order. Range 1-50. |
| `FeverAggressionPercent` | 80 | Step 43: the fever's peak share, ABSOLUTE (of the game's values): armed = max(his tired share, this); burning = tired + (this − tired) × left ÷ full, never below tired - it fades to his CURRENT tired share (which keeps sinking as he fights). A man already above it gets nothing from the fever. 0.144 → 0.115 on a charge. Range 0-200. Live. |
| `FeverContactMetres` | 5.0 | Step 43: the burn starts the first time an enemy is within this many metres of him (`GetNearbyEnemyAgentCount`, looked at every ~0.25 s); once burning it burns to empty, contact or not. Also the summary's "in contact". Range 1-20. |
| `FeverShieldDurationMultiplier` | 0.7 | Step 43: while a shield is in his off hand (read live at every look - step 21's weapon read) the store burns 1 ÷ this fast, so a shield man's fever lasts this share. Range 0.1-2. |
| `FeverSprintToClose` | true | Step 43: a feverish man (armed or burning) with sprint stored above his order's reserve (step 37), not sprinting, an enemy between `FeverSprintMinMetres` and `FeverSprintMaxMetres`, rolls every `FeverSprintCheckSeconds` with chance `FeverSprintChancePercent` × fever left ÷ full (full while armed) to start an AI sprint (step 36's `TryStartAi` - everything above the reserve; it ends at 3 m like any AI sprint). Needs `SprintEnabled` and `SprintAiEnabled`. Live. |
| `FeverSprintMinMetres` | 3.0 | Step 43: no fever sprint with an enemy closer than this (in melee). Range 0-10. |
| `FeverSprintMaxMetres` | 12.0 | Step 43: no fever sprint without an enemy within this. Range 1-40. |
| `FeverSprintCheckSeconds` | 1.0 | Step 43 (Anton: the sprint fades with the fever): seconds between a feverish man's fever-sprint rolls. Range 0.1-10. |
| `FeverSprintChancePercent` | 50 | Step 43: the fever-sprint chance per roll at a full fever; × fever left ÷ full (half the fever, half the chance; empty, none). 0 = never. Range 0-100. |
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
| `ShowFeverMarks` | true | Step 44 (Anton): while vanilla shows its formation markers (the same live flag as `ShowAltMarkerStats` - ALT held or the orders menu open), a small bar of ours above each AI man in a FEVER (§2 "Aggression and the fever"; armed or burning): bright orange while armed and at the burn's start, through yellow as the store empties, its fill = the fever left, gone when it is over (§3 item 3b). Calm men and you: nothing. Needs `FeverEnabled`. |
| `FeverMarksShowEnemy` | true | Step 44: the enemy's men in a fever get the mark too (vanilla's own per-man ALT icon is yours and your allies' only - ours is not). Off: yours and your allies' only. |
| `ShowAthleticsMarks` | true | Step 46 (Anton): while vanilla shows its formation markers (the same live flag - ALT held or the orders menu open), a small Athletics bar of ours above EVERY man's head (yours, allies', the enemy's with the next switch; AI men on foot and riders, heroes too; never you - you have your own bar): its fill = his points ÷ his FULL pool, the part his wounds hold dark red (the player bar's way), coloured by the bar's bands (`Bar*BelowPercent`: green at the peak line, blue just below, yellow / orange / red, empty red); just ABOVE his fever bar, at a fixed height whether he is in a fever or not (§3 item 3c). |
| `AthleticsMarksShowEnemy` | true | Step 46: the enemy's men get the Athletics bar too. Off: yours and your allies' only. |
| `FeverMarkMax` | 200 | Step 44: most fever marks drawn in one frame - the men NEAREST the camera (in front of it, at least 1.5 m away) when more are in a fever; the rest wait. Since step 46 the cap counts MEN MARKED - each with his Athletics bar and / or his fever bar (one cap for both, the key kept so no config migration is needed). Range 1-1000. |
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

Decisions 7–10 and the new parameters `DamageRandomOnShields`, `ExhaustedRecoverPercent`
(retired in 5c), `TargetBarMaxDistance` and `TargetBarLingerSeconds` (LATER, step 7), `FormationBarsAlways` (LATER, step 8),
`HudRefreshSeconds`, `FormationStatsRefreshSeconds` came out of step 2's research
(`docs/RESEARCH.md`, "Design implications"), settled by Claude while Anton was away
(2026-09-27) — every one is a default he can overturn.

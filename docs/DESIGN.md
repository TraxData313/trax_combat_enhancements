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
  empty him, and **it restarts the refill delay** — a man under attack is not resting. It is not an
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

All toggles, all on by default. **Built: items 1, 1b, 3 (step 20, in its "hold ALT" form) and 4. Item 2
is LATER** (Anton moved it off the build order, 2026-09-27): its design stays here, its settings wait in
"Planned parameters" below, and nothing of it is in the game, MCM or the config file.

1. **Player bar** (`ShowPlayerBar`): the player's Athletics bar near the vanilla health bar,
   in the spirit of RBM's posture bar (RBM = Realistic Battle Mod, confirmed by Anton —
   style reference only). Built in step 6.
1b. **Attack recovery bar** (`ShowAttackRecoveryBar`, Anton 2026-09-27: "above that bar add a bar
   'attack recovery' that empties when I attack and until it fills I can't attack; inside it add
   the secs delay added"): your no-attack pause (§2) made visible, just above the Athletics bar.
   Built in step 13 — below.
2. **LATER (step 7) — Looked-at NPC** (`ShowTargetBar`): a small bar for the fighter the player
   is aiming at / looking at, within `TargetBarMaxDistance`; it lingers `TargetBarLingerSeconds`
   after the aim leaves so it does not flicker. Aiming at a horse shows its rider.
3. **Hold ALT — numbers under the formation markers** (`ShowAltMarkerStats`; built in step 20, Anton
   2026-09-28: "I see the Athletics and health numbers above the troops when I hold ALT — it now shows
   me the troop count and distance"). This is step 8's "squad bars" in their "only while vanilla shows
   its formation markers" form. Built as below; an always-on form (`FormationBarsAlways`,
   `FormationBarHeight`) stays in "Planned parameters".
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
back, Battle pace (AI), Refill, Your Athletics bar, Orders menu strip, Formation markers (hold ALT), Advanced*; MCM shows
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
| `AthleticsEnabled` | true | Switch for Athletics (the stamina bar). |
| `AthleticsPoolFloor` | 50 | Smallest possible Athletics pool (for anyone with a low Athletics skill; 0 = none, never below 1 point). |
| `AthleticsPoolPerSkill` | 1.0 | Pool per point of Athletics skill (1.0 → the bar tops at the skill). |
| `AthleticsPeakPercent` | 75 | At or above this % of their own pool a fighter is at full strength (green); below it they weaken in a straight line to 0. |
| `HealthCapsAthletics` | true | Health left caps the usable pool. |
| `CostPerBlow` | 10 | Athletics points one blow costs before multipliers (points, whatever the pool). |
| `CostPerKickOrBash` | 3 | Step 18 (Anton): Athletics points one kick or shield bash costs before the hero and party-leader multipliers (3 / 2.25 / 1.7), once, when it starts, landed or not; never starts the attack pause. 0 = free (steps 5-17). |
| `CostPerShieldBlock` | 1 | Step 22 (Anton): Athletics points the DEFENDER pays for a melee blow he blocks with his shield on the correct side, before the hero and party-leader multipliers (1 / 0.75 / 0.56); once per blocked blow; restarts the refill delay; never an attack pause or a step back. 0 = free (steps 5-21). |
| `CostPerWrongSideShieldBlock` | 5 | Step 22 (Anton): the same for a blow his shield stops on the WRONG side (the engine's `CorrectSideShieldBlock` false) - 5 / 3.75 / 2.81. 0 = free. |
| `CostPerWeaponParry` | 2 | Step 22 (Anton): the same for a blow he blocks or parries with a weapon, no shield (a chamber block too) - 2 / 1.5 / 1.13. 0 = free. |
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
| `MinMoveSpeedMultiplier` | 0.7 | Top speed on foot at 0 Athletics (0.3 until step 14 - Anton: "too slow, unrealistic"; defaults.json 0.6 since step 20b - Anton's "sweet spot"; defaults.json 0.3 again since step 22 - Anton, for slower battles). |
| `MountMinSpeedMultiplier` | 1.0 | Horse top speed at the rider's 0 Athletics (1.0 = horses never slow). |
| `DamageBonusFollowsAthletics` | true | The damage upside shrinks with the attacker's Athletics below the peak. |
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
| `BarYellowBelowPercent` | 75 | Bar turns yellow at or below this % of the peak line (blue just below the line, green above it). |
| `BarOrangeBelowPercent` | 50 | Orange at or below this %. |
| `BarRedBelowPercent` | 25 | Red at or below this % (an empty bar is always red). |
| `ShowFormationSpread` | true | ± spread in the orders-menu strip and under the formation markers: the "± 8" and the lighter band (off: the average alone). |
| `FormationSpreadStdDevs` | 1.0 | Band width in standard deviations (the "± 8" is this width). |
| `ShowInOrderMenu` | true | The orders-menu strip: under each formation card, the men's average Athletics ± spread (bar + "72% ± 8"). |
| `ShowFormationHealth` | true | The orders-menu strip and the formation markers' numbers also show average health ("HP 81%"). |
| `OrderStripUnderCards` | true | true: the strip sits under the vanilla cards (read live) when they can be matched, else the compact panel. false: always the compact panel. |
| `ShowAltMarkerStats` | true | Step 20 (Anton): while vanilla shows its formation markers (ALT held or the orders menu open - the game's own rule, read live), each marked formation also gets "72% ± 8" (the men's average Athletics ± spread, coloured by their average f) and "HP 81%" just under its marker, over a slim bar (§3 item 3). Yours and your allies' always. |
| `AltMarkersShowEnemy` | true | Step 20: the enemy's formation markers get the numbers too (vanilla marks them) - knowing the enemy is tired is the tactical point. Off: yours and your allies' only. |
| `HudRefreshSeconds` | 0.1 | (Advanced) How often the player bar, the orders-menu strip and the formation markers' numbers update (the markers' numbers follow the markers every frame). |
| `PlayerBarWidth` | 205 | (Advanced) Length of your bar in UI pixels of the 1920 × 1080 layout (the game's UI scale applies); 205 = the inside of the vanilla health bar. |
| `PlayerBarHeight` | 12 | (Advanced) Thickness of your bar, UI pixels. |
| `PlayerBarOffsetRight` | 62 | (Advanced) Screen's right edge → your bar's right end, UI pixels (62 = under the vanilla health bar). |
| `PlayerBarOffsetBottom` | 30 | (Advanced) Screen's bottom edge → your bar's row (label, number, bar), UI pixels (30 = the Attack recovery bar fits above it, both just under the health and horse bars; it was 54 until step 13). |
| `RecoveryBarWidth` | 205 | (Advanced) Length of the Attack recovery bar, UI pixels (205 = your Athletics bar's). |
| `RecoveryBarHeight` | 14 | (Advanced) Thickness of the Attack recovery bar, UI pixels (the seconds inside it need about 12). |
| `RecoveryBarOffsetAbove` | 24 | (Advanced) Your Athletics bar's row → the Attack recovery bar's row (bottom to bottom), UI pixels (24 = right on top of it; it moves with your bar). |
| `OrderStripTextSize` | 13 | (Advanced) Font size of the strip's numbers, UI pixels. |
| `OrderStripTextOffset` | 1 | (Advanced) A card's bottom edge → the top of its numbers, UI pixels (they sit left and right of the vanilla order icons). |
| `OrderStripBarOffset` | 20 | (Advanced) A card's bottom edge → the top of its strip bar, UI pixels (20 = just under the vanilla order icons). |
| `OrderStripBarHeight` | 4 | (Advanced) Thickness of the strip bar, UI pixels. |
| `OrderStripSideMargin` | 2 | (Advanced) How far the numbers and the bar keep in from a card's sides, UI pixels. |
| `OrderPanelOffsetTop` | 80 | (Advanced) Screen's top edge → the fallback panel (centred), UI pixels. |
| `OrderPanelWidth` | 300 | (Advanced) Width of the fallback panel, UI pixels. |
| `AltMarkerTextSize` | 16 | (Advanced) Font size of the numbers under the formation markers, UI pixels (vanilla's marker count is 22; same font and outline - the brush NameMarker.Distance.Text). |
| `AltMarkerOffset` | 2 | (Advanced) A formation marker's bottom edge (its distance row) → the top of our numbers, UI pixels (negative = up over the marker). |
| `AltMarkerBarWidth` | 60 | (Advanced) Length of the slim bar under the marker's numbers, UI pixels (60 = the marker's distance row). |
| `AltMarkerBarHeight` | 3 | (Advanced) Thickness of that bar, UI pixels; 0 = no bar. |
| `FormationStatsRefreshSeconds` | 0.25 | (Advanced) How often formation averages and spreads are recomputed - every formation of every side since step 20. |
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

Decisions 7–10 and the new parameters `DamageRandomOnShields`, `ExhaustedRecoverPercent`
(retired in 5c), `TargetBarMaxDistance` and `TargetBarLingerSeconds` (LATER, step 7), `FormationBarsAlways` (LATER, step 8),
`HudRefreshSeconds`, `FormationStatsRefreshSeconds` came out of step 2's research
(`docs/RESEARCH.md`, "Design implications"), settled by Claude while Anton was away
(2026-09-27) — every one is a default he can overturn.

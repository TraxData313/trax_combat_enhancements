# Trax Combat Enhancements

*(working title)* — a combat mod for **Mount & Blade II: Bannerlord** (v1.4.8) that makes
every fight a little less predictable and a lot more about who is still fresh.

**Status: first release packaged, not published yet** — it goes on the Steam Workshop after its
playtest. Progress is tracked in [TASKS_TODO.md](TASKS_TODO.md) and [TASKS_DONE.md](TASKS_DONE.md).

**Requirements:** Mount & Blade II: Bannerlord **v1.4.8**, singleplayer. MCM (Mod Configuration
Menu) is optional.

## Install

- **Steam Workshop** — once it is released: subscribe, then enable *Trax Combat Enhancements* in
  the launcher. (The link goes here with the first upload.)
- **Manual, meanwhile** — build the zip with `powershell -ExecutionPolicy Bypass -File tools\package.ps1`
  (it lands in `dist\`), extract it into `<game>\Modules\` so it becomes
  `Modules\TraxCombatEnhancements\`, and enable it in the launcher.
- Keep only one copy: remove a manual install before subscribing (both carry the same module id).
  If two copies do end up enabled (say the Workshop one and a dev build), the first to load runs,
  the other stays out of the way, and the main menu says so.

Releasing it: [tools/WORKSHOP-UPLOAD.md](tools/WORKSHOP-UPLOAD.md).

## What it does

- **Damage randomness** — every hit that lands, melee or ranged, rolls ±50% damage.
  A 50-damage blow lands for anywhere between 25 and 75.
- **Athletics** — every fighter has an Athletics bar, his stamina, as big as his **Athletics
  skill** (never under 50). Each blow costs 10 points. The top quarter of the bar — above the
  peak line — is full strength; below it his lucky hits, his attack rate and his run speed fade,
  down to one attack where he used to make five, and 70% of his running pace, when it is empty. No slow motion: every swing,
  draw and throw plays at full speed, but after each attack a tired fighter must wait before the
  next one — you see your wait fill up in an Attack recovery bar (press attack too early and it
  just flashes; hold the button and you strike the moment it fills), the AI waits with its guard
  up. Blocking, kicks and moving are never held. Wounds cap the bar. Rest to
  refill it: one minute standing or walking, twice that running flat out — quick while the bar is
  low, slower as it fills (half the bar in about 25 seconds). Heroes pay less per
  blow, party leaders less again, and a hero's big Athletics skill means a big bar — the battle
  leans on its heroes.
- **Tired men step back** — after a swing, a tired AI soldier on foot may walk backwards out of the
  press, facing his enemy with his guard up, and rejoin his line a moment later: the more
  tired, the more often. The fresh take the blows. Field battles only.
- **See it** — your own Athletics bar under your health bar: the number, a mark at the peak line,
  green at full strength, then blue, yellow, orange and red as you tire, the part your wounds hold
  shown dark; above it the Attack recovery bar with the seconds of your wait inside it. And in the orders menu, a slim strip under each formation card: its men's average
  Athletics ± spread ("72% ± 8") and their average health ("HP 81%"). The strip reads the game's own
  cards, so it lines up at any resolution and UI scale, and with RTS Camera's order menu.
- **A master switch** — turn the whole mod off, even mid-battle, and the fight is pure vanilla, so
  you can play the same battle both ways and compare.

Every number is adjustable — in the **Mod Configuration Menu (MCM)** if you have it (optional:
the mod runs without it), or in a plain config file with an explanation beside each value
(`Documents\Mount and Blade II Bannerlord\Configs\TraxCombatEnhancements\config.json`). Changes
in MCM apply at once, even mid-battle; nothing ever needs a restart. The mod adds nothing to your
save, so it can be added or removed mid-campaign. Full spec: [docs/DESIGN.md](docs/DESIGN.md).

## Compatibility

- **Not compatible with RBM (Realistic Battle Mod)** — it brings its own posture and stamina
  systems. Use one or the other; the mod warns you at the main menu if it sees RBM.
- Built to sit alongside **RTS Camera** (and its Command System's order menu) and **War Sails**
  (no step backs on deck) — to be confirmed in the first playtest.
- MCM is optional.
- Safe to add or remove mid-campaign: it lives inside battles and writes nothing to the save.

## License

Public domain ([Unlicense](LICENSE)).

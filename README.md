# Trax Combat Enhancements

*(working title)* — a combat mod for **Mount & Blade II: Bannerlord** (v1.4.8) that makes
every fight a little less predictable and a lot more about who is still fresh.

**Status: in development.** Nothing to install yet — progress is tracked in
[TASKS_TODO.md](TASKS_TODO.md) and [TASKS_DONE.md](TASKS_DONE.md).

## What it does

- **Damage randomness** — every hit that lands, melee or ranged, rolls ±50% damage.
  A 50-damage blow lands for anywhere between 25 and 75.
- **Athletics** — every fighter has an Athletics bar, his stamina, as big as his Athletics
  skill (never under 50). Each blow costs 10 points. The top quarter of the bar is full
  strength; below it his lucky hits, his swing speed and his run speed fade, down to
  attacking at 20% speed when it is empty. Wounds cap the bar. Rest to refill it: one minute
  standing or walking, twice that running flat out. Heroes pay less per blow, party leaders
  less again, and a hero's big Athletics skill means a big bar — the battle leans on its
  heroes.
- **Tired men step back** — after a swing, a tired AI soldier on foot may step back out of the
  press, facing his enemy with his guard up, and rejoin his line a moment later: the more
  tired, the more often. The fresh take the blows.
- **See it** — your own Athletics bar, the bar of whoever you're looking at, and your
  squads' average Athletics (± spread) floating above them and in the orders menu.
- **A master switch** — turn the whole mod off mid-battle and the fight is pure vanilla, so
  you can play the same battle both ways and compare.

Every number is adjustable — in the Mod Configuration Menu if you have it, or in a plain
config file with an explanation beside each value. Full spec: [docs/DESIGN.md](docs/DESIGN.md).

## Compatibility

**Not compatible with RBM (Realistic Battle Mod)** — it brings its own posture and stamina
systems. Use one or the other.

## License

Public domain ([Unlicense](LICENSE)).

# Trax Combat Enhancements

*(working title)* — a combat mod for **Mount & Blade II: Bannerlord** (v1.4.8) that makes
every fight a little less predictable and a lot more about who is still fresh.

**Status: in development.** Nothing to install yet — progress is tracked in
[TASKS_TODO.md](TASKS_TODO.md) and [TASKS_DONE.md](TASKS_DONE.md).

## What it does

- **Damage randomness** — every hit that lands, melee or ranged, rolls ±50% damage.
  A 50-damage blow lands for anywhere between 25 and 75.
- **Endurance** — every fighter has a pool that each blow drains (10 of 100). Empty means
  attacking at 20% speed until you catch your breath. Rest to refill it: one minute standing
  still, two while moving. Heroes pay less per blow, and party leaders less again — the
  battle leans on its heroes.
- **See it** — your own endurance bar, the bar of whoever you're looking at, and your
  squads' average endurance (± spread) floating above them and in the orders menu.

Every number is adjustable — in the Mod Configuration Menu if you have it, or in a plain
config file with an explanation beside each value. Full spec: [docs/DESIGN.md](docs/DESIGN.md).

## Compatibility

**Not compatible with RBM (Realistic Battle Mod)** — it brings its own posture and stamina
systems. Use one or the other.

## License

Public domain ([Unlicense](LICENSE)).

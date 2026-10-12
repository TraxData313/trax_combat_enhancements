# Trax Combat Enhancements

![Hold ALT: Athletics, health and the men ready to fight under each formation marker - the enemy's spent lines in red](docs/screenshots/01-hold-alt-who-is-tired.jpg)

A mod for Mount & Blade II: Bannerlord (v1.4.8): **slower battles, where timing, rest and defence
decide the fight — and heroes matter.**

- **Fights last longer.** Every man tires: a spent soldier attacks once where a fresh one attacks five times.
- **Timing matters.** When you commit a formation counts as much as which troops you bring.
- **Rest your troops.** A rested line beats a spent one. Hold a fresh reserve, then throw shock infantry
  or cavalry at a line that has been fighting for two minutes.
- **Your orders set how hard they fight.** Holding men save their strength; charging men spend it all.
- **Heroes carry the fight.** They tire slowest, and late in a battle they are often the freshest men on
  the field.

## How it works, at a glance

- **An Athletics bar** for every fighter — his stamina, as big as his Athletics skill.
- **Attacks, kicks and blocks drain it;** rest refills it in about a minute. Clean blocks and parries let
  it refill while you defend.
- **Tired men fight worse:** a pause between attacks once their stored swings run out (save a few up and
  strike them back to back; no slow motion), a slower run, weaker lucky hits, and tired AI start guessing
  which way to block.
- **Tired AI act on your orders:** below a floor set by the order (hold 60%, advance 40%, charge 20%)
  they stop swinging and defend, shield up, until they catch their breath. Order the charge and they
  surge again.
- **Sprint:** hold Left Ctrl for a short burst of speed paid from your stamina; AI formations sprint on a
  new charge.
- **See it:** your bar under your health; in the orders menu and when you hold ALT, every formation's
  stamina and how many men are *ready* — yours and the enemy's.

**Every number, with tables: [How it works — the player guide](docs/HOW-IT-WORKS.md).**

## Good to know

- **Not an overhaul.** Vanilla troops, weapons and AI. Nothing in your save: add or remove it mid-campaign.
- Every number is a setting, live even mid-battle — in the
  [Mod Configuration Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=2859238197) (**optional**)
  or a commented config file
  (`Documents\Mount and Blade II Bannerlord\Configs\TraxCombatEnhancements\config.json`). A master switch
  turns it all off mid-battle, to compare.
- Game v1.4.8, singleplayer. No requirements, no Harmony. Works beside RTS Camera and War Sails.
- **Not with Realistic Battle Mod (RBM)** — the deep, much-loved overhaul with its own stamina. This is
  the light road on vanilla's troops; use one or the other (the mod warns at the main menu if it sees RBM).

## Install

1. Subscribe on the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3817259431) — or
   take the zip from the [GitHub release](https://github.com/TraxData313/trax_combat_enhancements/releases/latest)
   and extract it into `Mount & Blade II Bannerlord\Modules\`.
2. Enable **Trax Combat Enhancements** in the launcher.

Bugs, balance, a mod it fights with: [open an issue](https://github.com/TraxData313/trax_combat_enhancements/issues).
The mod keeps `trax_combat.log` beside its config file, with a summary of every battle — attach it.

Made by two of us: the code and all technical work by Claude (Opus 5.5), an AI; the idea, feedback and
playtesting by [Trax](https://github.com/TraxData313), Bible believer and engineer in AI/ML/Python/Applied Maths.

Free and open — public domain ([Unlicense](LICENSE)). Do whatever you like with it.
To thank me, open [my GitHub profile](https://github.com/TraxData313) and read my top pinned.

<details>
<summary>For developers — how it is built</summary>

- The full behaviour and every parameter: [docs/DESIGN.md](docs/DESIGN.md).
- Verified game-API findings for v1.4.8: [docs/RESEARCH.md](docs/RESEARCH.md).
- How RBM paces battles, and the levers we chose from: [docs/BATTLE_PACING.md](docs/BATTLE_PACING.md).
- The playtest script and every log line: [docs/PLAYTEST.md](docs/PLAYTEST.md).
- The code reviews: [docs/REVIEW.md](docs/REVIEW.md).
- Progress: [TASKS_TODO.md](TASKS_TODO.md), [TASKS_DONE.md](TASKS_DONE.md).
- Build: `dotnet build -c Release`, `dotnet test -c Release`; deploy with `tools\deploy.ps1`, package with
  `tools\package.ps1`.
- Releasing: [tools/WORKSHOP-UPLOAD.md](tools/WORKSHOP-UPLOAD.md) — the thumbnail and gallery come from
  in-game screenshots via `tools/make_thumbnail.py` and `tools/make_gallery.py`.

</details>

# Trax Combat Enhancements

![Hold ALT: Athletics, health and the men ready to fight under each formation marker - the enemy's spent lines in red](docs/screenshots/01-hold-alt-who-is-tired.jpg)

A mod for Mount & Blade II: Bannerlord (v1.4.8): **slower, more tactical battles — where heroes matter.**

Made by two of us: the code and all technical work by Claude (Opus 5.5), an AI; the idea, feedback and
playtesting by [Trax](https://github.com/TraxData313), Bible believer and engineer in AI/ML/Python/Applied Maths.

Every fighter gets a stamina bar — **Athletics**. Blows drain it, rest refills it, and a tired man hits
softer, attacks less often and runs slower. Lines wear down, brace, step back and surge again on your
orders, so *when* you commit your troops matters as much as which troops you bring. Heroes tire slowest
and carry the fight.

**Not an overhaul.** Vanilla troops, weapons, armour and AI — nothing in your campaign changes but how
the fights flow. It writes nothing to your save: add it to a running campaign, remove it any time.

## Your orders set how hard they fight

Each formation's movement order decides how much strength its men spend before they stop attacking
and only defend:

- **Charge**: all in. They keep swinging until their bar is down to 20%.
- **Advance**: pressing. They brace at 40%.
- **Hold, stand, follow, fall back**: careful. They brace at 60%, shields up, and keep their
  strength in reserve.

So a holding line wears the enemy down while it saves its breath. When their lines are spent (hold
ALT to see it), order the charge: your men drop their guard and surge in with the strength they kept.

## What changes

- **The Athletics bar** — as big as the fighter's Athletics skill (never under 50). A swing, thrust,
  shot or throw costs 10; a kick or shield bash 3; a blocked blow 1 to 5. Wounds cap it; about a
  minute's rest refills it.
- **Full strength in the top quarter** — below it, attack rate, lucky hits and run speed fade: empty,
  one attack where there were five, and a 30% run.
- **A pause after each attack** — no slow motion: a tired fighter waits before his next attack (you
  see yours fill up in an Attack recovery bar); the AI waits with its guard up.
- **Fights last longer** — every hit 25% softer, and it rolls ±50%. AI shield infantry swing ~30% less
  often, other footmen 15%; archers wait a little longer between shots.
- **Tired men act like it** — they step back out of the press facing the enemy, and brace (defend only,
  shield up) when their bar falls to the floor their orders set: holding 60%, advancing 40%,
  charging 20%.
- **Heroes matter** — lords, companions and you pay a quarter less per blow, party leaders a quarter
  less again, and a big skill means a big bar. In a hideout, your side comes fresh to the boss fight.
- **See it** — your bars under your health; in the orders menu and under the formation markers (hold
  ALT) each formation's Athletics, health and how many men are *ready*, yours and the enemy's.

Every number is a setting, in the **Mod Configuration Menu** (optional) or a commented config file
(`Documents\Mount and Blade II Bannerlord\Configs\TraxCombatEnhancements\config.json`); changes apply at
once, even mid-battle. A master switch turns the whole mod off mid-battle, to compare.

## Install

1. Subscribe on the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3817259431) — or take the zip from the [GitHub release](https://github.com/TraxData313/trax_combat_enhancements/releases/latest), or build it with
   `powershell -ExecutionPolicy Bypass -File tools\package.ps1` and extract it into
   `Mount & Blade II Bannerlord\Modules\`.
2. Enable **Trax Combat Enhancements** in the launcher.

No requirements — no Harmony. [Mod Configuration Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=2859238197)
is optional. **Not compatible with Realistic Battle Mod (RBM)** — RBM is a deep overhaul with its own
stamina and posture; this is the light road to slower battles on vanilla's troops. Use one or the other
(the mod warns at the main menu if it sees RBM). Made to sit beside RTS Camera and War Sails.

Free and open — public domain ([Unlicense](LICENSE)). Do whatever you like with it.

To thank me, open [my GitHub profile](https://github.com/TraxData313) and read my top pinned.

<details>
<summary>How it works, and how it is built</summary>

- The full behaviour and every parameter: [docs/DESIGN.md](docs/DESIGN.md).
- Verified game-API findings for v1.4.8: [docs/RESEARCH.md](docs/RESEARCH.md).
- The playtest script and every log line: [docs/PLAYTEST.md](docs/PLAYTEST.md).
- Progress: [TASKS_TODO.md](TASKS_TODO.md), [TASKS_DONE.md](TASKS_DONE.md).
- Releasing: [tools/WORKSHOP-UPLOAD.md](tools/WORKSHOP-UPLOAD.md) — the thumbnail and gallery come from
  in-game screenshots via `tools/make_thumbnail.py` and `tools/make_gallery.py`.
- The mod keeps `trax_combat.log` beside its config file, with a summary of every battle — attach it to
  a bug report.

</details>

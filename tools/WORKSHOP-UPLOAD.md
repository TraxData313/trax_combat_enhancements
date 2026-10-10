# Releasing on the Steam Workshop

**Status: not uploaded yet.** The package is one command away and the upload one more — but
publishing is Anton's call, and it comes only after his playtest (`docs/PLAYTEST.md`).

Item: **not created yet** — after the first upload, write its number here, in
`tools\WorkshopUpdate.xml` and in README's Install section.

The upload path is Bannerlord's **own official uploader** —
`TaleWorlds.MountAndBlade.SteamWorkshop.exe` in the game's `bin\Win64_Shipping_Client` — the
same one both sibling mods ship with. It rides the **already-logged-in Steam client**: no
SteamCMD, no password, no Steam Guard.

## First release (once)

1. **Playtest done, Anton says yes.** Nothing below happens before that.
2. **Stamp the version — once.** `module\SubModule.xml` → `<Version value="vX.Y.Z" />` is the
   ONLY place (v1.0.0, the first public release, stamped 2026-10-10 - Anton's call).
   Both DLLs take their version from it (`Directory.Build.props`). Commit it **before**
   packaging: the DLL carries the commit it was built from, and the `[load]` line prints it.
3. **Package** (from the repo root):
   ```powershell
   powershell -ExecutionPolicy Bypass -File tools\package.ps1
   ```
   Build → unit tests → AssemblyGuard (hard) → offline smoke → `dist\TraxCombatEnhancements`
   from scratch + `dist\TraxCombatEnhancements_vX.Y.Z.zip`, and it prints every file with its
   size. If a zip of that version is already there from a test package, add `-Force`.
   Its default game path is on `D:`; with the game on `C:` (this PC, 2026-10-10) pass both folders:
   ```powershell
   powershell -ExecutionPolicy Bypass -File tools\package.ps1 -GameFolder "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord" -McmBinFolder "C:\Program Files (x86)\Steam\steamapps\workshop\content\261550\2859238197\bin\Win64_Shipping_Client"
   ```
4. **Check the images** — the preview `tools\preview_thumbnail.jpg` and the gallery
   `docs\screenshots\*.jpg` (see "The images" below). Both are built from Anton's F12 shots;
   rebuild only after new screenshots.
5. **Steam running and logged in, Steam Cloud on** — for the account AND for Bannerlord
   (Library → Bannerlord → Properties → General → Steam Cloud). The uploader refuses otherwise.
6. **Run the uploader with the CREATE file:**
   ```powershell
   & "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.SteamWorkshop.exe" "C:\Users\Asus ROG\Documents\GitHub\trax_combat_enhancements\tools\WorkshopCreate.xml"
   ```
   Success = **"Item created. Item ID is …"** and **"Uploading done!"** in the output. It creates
   the item **Private**, titled from `SubModule.xml <Name>` ("Trax Combat Enhancements"), with a
   short pitch as its description, the tags Utility, UI, Native, Singleplayer, v1.4.8, and the
   preview image.
7. **Record the item id** — the number from "Item ID is …" (also in the item page's URL):
   `tools\WorkshopUpdate.xml` (`ITEM_ID`), the "Item:" line above, README's Install link. Commit.
   **Never run `WorkshopCreate.xml` again** — it would make a second item. If the upload failed
   AFTER "Item created", the empty item exists: use its id with `WorkshopUpdate.xml`.
8. **The full description** — the create task sends only the short pitch. Either paste
   `tools\STEAM-DESCRIPTION.bbcode` whole on the item page (*Edit title & description*; 6.0 KB,
   Steam's cap is 8000), or let the uploader send it with the first update (the sibling's way):
   ```powershell
   $task = python tools\make_workshop_update.py --notes "First release."
   & "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.SteamWorkshop.exe" $task
   ```
   (it re-uploads the module too; needs the item id in `WorkshopUpdate.xml` first).
   **On the item page** (Owner Controls) — the uploader cannot do these: *Add/edit images &
   videos* → the five gallery shots, `01-` to `05-` in `docs\screenshots`, in that order (the list
   is under "The images"). Do NOT list MCM under Required items — it is optional; there are no
   required items (no Harmony). If Steam asks to accept the Workshop legal agreement, that is
   Anton's click.
9. **Sanity check the exact build players get**: subscribe to the (still private) item, and in
   the launcher enable **Trax Combat Enhancements** — not "(dev)". `trax_combat.log` then says
   `module: TraxCombatEnhancements (the release)` and the version. Enabling both copies is safe
   now: the first to load runs, the other stands down, and one yellow message says so.
10. **Flip it Public** on the item page (Owner Controls → Visibility).

## Every release after the first

The release rhythm (CLAUDE.md "Release"): work collects in `main` without version bumps; the
version is stamped once, on release day.

1. Playtest what changed. Anton says yes.
2. Bump `<Version>` in `module\SubModule.xml` once, commit.
3. `powershell -ExecutionPolicy Bypass -File tools\package.ps1`
4. `tools\WorkshopUpdate.xml`: set `<ChangeNotes Value="…"/>` — short, for players (a `"` inside is
   written `&quot;`). Bump the `v1.4.8` tag if the supported game version moved (ground truth:
   `<game>\bin\Win64_Shipping_Client\Version.xml`).
5. Steam open, then:
   ```powershell
   & "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.SteamWorkshop.exe" "C:\Users\Asus ROG\Documents\GitHub\trax_combat_enhancements\tools\WorkshopUpdate.xml"
   ```
   Success = **"Uploading done!"**.
6. A changed description: paste it on the page by hand (the update file never touches it), or
   build the task with `python tools\make_workshop_update.py --notes "vX.Y.Z - …"` — it sends
   `STEAM-DESCRIPTION.bbcode` (escaped, line breaks as `&#10;`) and, with `--visibility`, the
   visibility; run the uploader on the path it prints.
7. The locally subscribed copy shows the OLD version for a while (Steam re-downloads on its own
   schedule). That is not a failed upload.

## Uploader quirks (decompiled 2026-09-27 from the v1.4.8 exe — the same as the siblings found)

- **`<Tasks>` must be the first node of the task file.** An `<?xml?>` declaration above it makes
  the tool parse ZERO tasks and exit as if it had succeeded. A comment directly under `<Tasks>`
  crashes it (every child is taken for a task). Comments are safe only inside
  `<GetItem>` / `<UpdateItem>` / `<Tags>`.
- **The title comes from `<ModuleFolder>`'s `SubModule.xml <Name>`**, never from the task file
  (`package.ps1` refuses any name but "Trax Combat Enhancements").
- **Start-up checks**: Steam running, logged in, Steam Cloud enabled for the account and for the
  app — "Could not initialize Steam", "Steam user is not logged in", "Cloud is not enabled for …".
- **Droppings**: it writes `steam_appid.txt` and `steam_workshop_uploader.txt` (its own log of
  the run) into the working directory — git-ignored at the repo root.
- **The ending**: it finishes on a press-any-key read. In a real console it waits for a key; run
  non-interactively (e.g. from Claude's shell) it crashes right there — harmless. **Judge by the
  output, never the exit code**: "Uploading done!" = success; "Creating item failed…",
  "Uploading item failed…", "Application crashed with exception…" = not.
- **What an update sets**: the title (from `<Name>`), the content (the whole `<ModuleFolder>`), the
  tags (it REPLACES the list — keep all five), the change notes (default "Minor changes."), the
  preview only if an `<Image>` line is present, the description and visibility only if present
  (`WorkshopUpdate.xml` has neither, so page edits survive).
- **`ItemId` still `ITEM_ID`** → a FormatException before anything is uploaded — a safe failure.
- **Visibility** values: `Private`, `Public`, `FriendsOnly` (any case).
- **Limits**: the description 8000 bytes (Steam); the preview image under 1 MB.

## What gets uploaded

The whole `dist\TraxCombatEnhancements` folder, exactly what `package.ps1` built and listed
(v0.1.0 test package, 2026-10-10: 9 files, 833,104 bytes; the zip 328,497 bytes):

```
SubModule.xml                                  the release identity (Id TraxCombatEnhancements)
bin\Win64_Shipping_Client\TraxCombatEnhancements.dll + .pdb
bin\Win64_Shipping_Client\TraxCombat.Core.dll        + .pdb
GUI\Prefabs\TraxPlayerAthleticsBar.xml, TraxAttackRecoveryBar.xml, TraxOrderStrip.xml, TraxAltMarkers.xml
```

The `.pdb` files give the `[error]` stacks in players' logs their line numbers. Never MCM,
Newtonsoft.Json (the game ships its own) or a game DLL — `package.ps1` checks the folder against
that list and fails on anything else. Subscribers get it under
`steamapps\workshop\content\261550\<item id>\`.

## The images

Anton's F12 screenshots (Steam saves them under
`C:\Program Files (x86)\Steam\userdata\<id>\760\remote\261550\screenshots`) are copied into
`screenshots\originals\` — git-ignored, as the sibling does; only the built images are committed.
The images of v1.0.0 came from the 2026-10-10 20:52–20:54 battle shots (the 20:58+ shots of the same
batch are another mod's Party Steward screen).

**The preview** — `tools\preview_thumbnail.jpg` (1024 × 1024, ~275 KB), the image `WorkshopCreate.xml`
uploads:
```powershell
python tools\make_thumbnail.py
```
It cuts the battle, the hold-ALT close-up (a spent enemy line "ready 1/9" beside your fresh shield
wall "ready 37/53") and your two bars out of the shots, paints out stray labels, fills the template
`tools\preview_thumbnail.html` and renders it with headless Edge. New shots: change `SHOTS` /
`ERASE` at the top of the script (file + crop box in pixels), then LOOK at the result — also at
256 px, the size of a Workshop tile.

**The gallery** — `docs\screenshots\` (2560 px wide, under 1 MB each; README shows the first):
```powershell
python tools\make_gallery.py
```
Added on the item page by hand, in this order:

| File | Shows |
|---|---|
| `01-hold-alt-who-is-tired.jpg` | Hold ALT: Athletics, health and "ready" under every formation marker — the enemy's spent lines red |
| `02-shield-wall-and-archers.jpg` | A shield wall with archers behind it; the orders strip and both sides' ALT labels |
| `03-orders-menu-strip.jpg` | The orders menu: each card's Athletics ± spread, health, ready count |
| `04-your-bars.jpg` | Your Athletics bar and the Attack recovery bar under your health |
| `05-hero-in-the-field.jpg` | A hero on horseback |

## Manual install (the zip)

`dist\TraxCombatEnhancements_vX.Y.Z.zip` holds the module folder at its root: extract it into
`<game>\Modules\` so it becomes `Modules\TraxCombatEnhancements\…`, then enable it in the
launcher. It can go on a GitHub release for players outside Steam (not done yet — Anton's call).
Never keep a manual copy AND the Workshop copy of the same version: same module id twice.

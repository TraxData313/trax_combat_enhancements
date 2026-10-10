r"""Builds the square Steam Workshop preview: tools\preview_thumbnail.jpg (1024x1024, under Steam's 1 MB).

    python tools\make_thumbnail.py

Adapted from ..\better-skirmisher-separation\tools\make_thumbnail.py (same gold-frame / Palatino style):
1. Cuts the panels out of Anton's F12 screenshots in screenshots\originals\ (git-ignored, like the sibling's):
   the battle behind everything, the ALT-marker close-up (a tired enemy line "ready 1/9" beside your fresh
   shield wall "ready 37/53") and your own Attack recovery + Athletics bars. Panels go to
   screenshots\thumbnail build\.
2. Fills tools\preview_thumbnail.html (the template: PANEL_* placeholders) and renders it with headless
   Edge (or Chrome) at 1024x1024.
3. Saves tools\preview_thumbnail.jpg (the image WorkshopCreate.xml uploads), stepping JPEG quality down until
   it fits under 1 MB (a PNG of a photo is ~1.5 MB). Look at the result before you commit it.
Needs Pillow and Edge or Chrome. New screenshots: change SHOTS below (source file + crop box in pixels).
"""
from __future__ import annotations

import io
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageEnhance, ImageFilter

REPO = Path(__file__).resolve().parent.parent
ORIGINALS = REPO / "screenshots" / "originals"
WORK = REPO / "screenshots" / "thumbnail build"
OUT = REPO / "tools" / "preview_thumbnail.jpg"
STEAM_LIMIT = 1024 * 1024

# placeholder -> (screenshot, crop box (x0, y0, x1, y1) in its 3440x1440 pixels, output size, sharpen)
SHOTS = {
    # the battle: your shield infantry and archers, the player's back - 2026-10-10 20:53:10
    "PANEL_BATTLE": ("20261010205310_1.jpg", (480, 612, 1900, 1440), (1130, 659), False),
    # hold ALT: the enemy's spent line (red, ready 1/9) beside your shield wall (ready 37/53) - 20:53:59
    "PANEL_ALT": ("20261010205359_1.jpg", (2856, 640, 3296, 832), (660, 288), True),
    # your Attack recovery + Athletics bars, bottom right of the screen - 20:52:26
    "PANEL_BARS": ("20261010205226_1.jpg", (2884, 1342, 3364, 1398), (720, 84), True),
}

# game labels to paint out before cropping (box in source pixels, clone source = the same rows dx pixels to the right, negative = left),
# like the sibling's erase_count: two far formations' ALT labels above the troops in the battle shot
ERASE = {
    "PANEL_BATTLE": [((1015, 596, 1250, 712), 240)],
    # the troop counts "9" and "53" above the two markers (the caption tab sits on that edge)
    "PANEL_ALT": [((2946, 654, 2982, 694), -44), ((3120, 634, 3164, 670), 52)],
}

BROWSERS = [
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
]


def cut(name: str, shot: str, box: tuple[int, int, int, int], size: tuple[int, int], sharpen: bool) -> Path:
    src = ORIGINALS / shot
    if not src.exists():
        sys.exit(f"missing screenshot {src} - copy Anton's F12 shots from "
                 r"<Steam>\userdata\<id>\760\remote\261550\screenshots into screenshots\originals")
    img = Image.open(src).convert("RGB")
    for (x0, y0, x1, y1), dx in ERASE.get(name, []):
        img.paste(img.crop((x0 + dx, y0, x1 + dx, y1)), (x0, y0))
    img = img.crop(box).resize(size, Image.LANCZOS)
    if sharpen:
        img = img.filter(ImageFilter.UnsharpMask(radius=2, percent=110, threshold=2))
    else:
        img = ImageEnhance.Contrast(ImageEnhance.Color(img).enhance(1.08)).enhance(1.05)
    out = WORK / f"{name.lower()}.png"
    img.save(out)
    return out


def main() -> None:
    WORK.mkdir(parents=True, exist_ok=True)
    html = (REPO / "tools" / "preview_thumbnail.html").read_text(encoding="utf-8")
    for key, spec in SHOTS.items():
        html = html.replace(key, cut(key, *spec).as_uri())
    page = WORK / "preview_thumbnail.html"
    page.write_text(html, encoding="utf-8")

    browser = next((b for b in BROWSERS if Path(b).exists()), None) or shutil.which("msedge") or shutil.which("chrome")
    if not browser:
        sys.exit("Edge or Chrome not found.")
    png = WORK / "render.png"
    png.unlink(missing_ok=True)
    subprocess.run([browser, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
                    "--allow-file-access-from-files", "--window-size=1024,1024", f"--screenshot={png}",
                    page.as_uri()], check=True, capture_output=True, timeout=120)
    img = Image.open(png).convert("RGB").crop((0, 0, 1024, 1024))

    for q in (92, 90, 88, 85, 82, 78, 74, 70):        # a photo: JPEG, stepped down until under 1 MB
        buf = io.BytesIO()
        img.save(buf, "JPEG", quality=q, optimize=True, progressive=True)
        if buf.tell() < STEAM_LIMIT:
            break
    OUT.write_bytes(buf.getvalue())
    print(f"{OUT}: {img.width}x{img.height}, {buf.tell() // 1024} KB, q{q}")

if __name__ == "__main__":
    main()

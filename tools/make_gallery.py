r"""Builds the Steam Workshop gallery (the extra screenshots on the item page) from Anton's F12 shots.

    python tools\make_gallery.py

Reads screenshots\originals\ (git-ignored raw shots, 3440x1440) and writes docs\screenshots\NN-name.jpg
(committed): 2560 px wide, JPEG stepped down until under 1 MB. The order below IS the gallery order; the
captions go into tools\WORKSHOP-UPLOAD.md's gallery list (Steam has no caption field for an image - the
description carries the story). The uploader cannot add gallery images: they go on the item page by hand
(Owner Controls -> "Add/edit images & videos"), as the sibling better-skirmisher-separation does.
Needs Pillow.
"""
from __future__ import annotations

import io
import sys
from pathlib import Path

from PIL import Image

REPO = Path(__file__).resolve().parent.parent
ORIGINALS = REPO / "screenshots" / "originals"
OUT = REPO / "docs" / "screenshots"
WIDTH = 2560
LIMIT = 1024 * 1024

# (output name, source shot, caption) - gallery order
GALLERY = [
    ("01-hold-alt-who-is-tired.jpg", "20261010205359_1.jpg",
     "Hold ALT: Athletics, health and the men ready to fight under every formation marker - "
     "the enemy's spent lines in red."),
    ("02-shield-wall-and-archers.jpg", "20261010205310_1.jpg",
     "A shield wall with archers behind it; the orders-menu strip and the ALT labels on both sides."),
    ("03-orders-menu-strip.jpg", "20261010205234_1.jpg",
     "The orders menu: each formation card shows its men's Athletics, spread, health and ready count."),
    ("04-your-bars.jpg", "20261010205226_1.jpg",
     "Your Athletics bar and the Attack recovery bar, under your health."),
    ("05-hero-in-the-field.jpg", "20261010205405_1.jpg",
     "A hero on horseback: a big Athletics skill means a big bar."),
]


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    for name, shot, caption in GALLERY:
        src = ORIGINALS / shot
        if not src.exists():
            sys.exit(f"missing {src}")
        img = Image.open(src).convert("RGB")
        img = img.resize((WIDTH, round(img.height * WIDTH / img.width)), Image.LANCZOS)
        for q in (90, 88, 85, 82, 78, 74, 70):
            buf = io.BytesIO()
            img.save(buf, "JPEG", quality=q, optimize=True, progressive=True)
            if buf.tell() < LIMIT:
                break
        (OUT / name).write_bytes(buf.getvalue())
        print(f"{name}: {img.width}x{img.height}, {buf.tell() // 1024} KB, q{q} - {caption}")


if __name__ == "__main__":
    main()

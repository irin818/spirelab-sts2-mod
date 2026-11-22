#!/usr/bin/env python3
"""Reproducibly generate pixel-locked RGBA animation mask for C01 Potion Toss (`c01.png`, 500x380).

Never modifies original art `SpireLab/SpireLab/images/witch/cards/c01.png`.
Outputs `SpireLab/SpireLab/images/witch/cards/animated/c01/c01_masks.png`:
  R: Crimson potion liquid inside floating glass flask
  G: Floating glass flask body, rim highlights & cork
  B: Swirling crimson alchemy ribbons & floating spark droplets (strictly excluding Witch & hand)
  A: Gently floating flask motion region (strictly 0 on Witch face, hat, hair, torso, hand)
"""

from __future__ import annotations

import os
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "SpireLab/SpireLab/images/witch/cards/c01.png"
OUT = ROOT / "SpireLab/SpireLab/images/witch/cards/animated/c01/c01_masks.png"


def smoothstep(e0: float, e1: float, x: np.ndarray) -> np.ndarray:
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def blur_f(arr: np.ndarray, radius: float) -> np.ndarray:
    im = Image.fromarray(np.clip(arr * 255.0, 0, 255).astype(np.uint8))
    im = im.filter(ImageFilter.GaussianBlur(radius=radius))
    return np.asarray(im, dtype=np.float32) / 255.0


def main() -> None:
    im = Image.open(SRC).convert("RGB")
    arr = np.asarray(im, dtype=np.float32)
    h, w = arr.shape[:2]
    assert (w, h) == (500, 380), f"Unexpected c01.png size: {w}x{h}"

    r, g, b = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2]
    lum = 0.299 * r + 0.587 * g + 0.114 * b
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)

    # Floating glass flask bulb centered around (418, 102), radius ~58; neck + cork up to (468, 22)
    bulb_d = np.sqrt(((xx - 418.0) / 60.0) ** 2 + ((yy - 102.0) / 56.0) ** 2)
    # Tilted neck along vector from (438, 60) to (472, 16)
    nx = (xx - 455.0) * 0.78 - (yy - 38.0) * 0.62
    ny = (xx - 455.0) * 0.62 + (yy - 38.0) * 0.78
    neck_d = np.maximum(np.abs(nx) / 22.0, np.abs(ny) / 36.0)
    flask_zone = np.maximum(1.0 - smoothstep(0.92, 1.06, bulb_d), 1.0 - smoothstep(0.85, 1.15, neck_d))

    # Strict Witch & outstretched hand protection (left side + lower-center hand)
    witch_protect = 1.0 - smoothstep(265.0, 305.0, xx)
    hand_d = np.sqrt(((xx - 332.0) / 96.0) ** 2 + ((yy - 295.0) / 94.0) ** 2)
    hand_protect = 1.0 - smoothstep(0.75, 1.08, hand_d)
    safe_right = np.clip(1.0 - np.maximum(witch_protect, hand_protect), 0.0, 1.0)

    # R: Crimson potion liquid inside lower ~62% of the glass bulb (y > 80 inside bulb)
    is_crimson = ((r - g) > 22.0) & (r > 55.0)
    liquid_band = smoothstep(78.0, 96.0, yy) * (1.0 - smoothstep(0.86, 0.98, bulb_d))
    liquid_mask = blur_f(liquid_band * np.where(is_crimson | (lum > 45.0), 1.0, 0.35), 2.0) * safe_right

    # G: Glass flask specular rim & bright reflections
    bright_glass = smoothstep(105.0, 210.0, lum) * flask_zone
    glass_rim = smoothstep(0.68, 0.95, bulb_d) * (1.0 - smoothstep(0.95, 1.08, bulb_d))
    glass_mask = np.clip(blur_f(np.maximum(bright_glass, glass_rim * 0.85), 1.5), 0.0, 1.0) * safe_right

    # B: Swirling crimson alchemy ribbons & floating spark droplets outside the flask & off Witch/hand
    ribbon_crimson = ((r - g) > 35.0) & (r > 95.0) & (bulb_d > 0.96)
    ribbon_mask = np.clip(blur_f(ribbon_crimson.astype(np.float32), 1.6) * 1.45, 0.0, 1.0) * safe_right

    # A: Gently floating flask motion envelope (strictly around flask only, 0 on Witch & hand)
    float_zone = np.maximum(1.0 - smoothstep(0.85, 1.24, bulb_d), 1.0 - smoothstep(0.85, 1.32, neck_d))
    float_mask = blur_f(float_zone, 4.5) * safe_right

    rgba = np.stack([liquid_mask, glass_mask, ribbon_mask, float_mask], axis=-1)
    rgba_u8 = np.clip(np.round(rgba * 255.0), 0, 255).astype(np.uint8)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(rgba_u8).save(OUT, optimize=True)
    print(f"Wrote {OUT.relative_to(ROOT)} ({OUT.stat().st_size} bytes)")
    for i, label in enumerate(["R(liquid)", "G(glass)", "B(ribbons)", "A(float)"]):
        print(f"  {label}: {(rgba_u8[:, :, i] > 20).sum()} px > 20, max={rgba_u8[:, :, i].max()}")

    audit_dir = os.environ.get("WITCH_MASK_AUDIT_DIR")
    if audit_dir:
        colors = [
            np.array([255.0, 75.0, 110.0]),
            np.array([150.0, 215.0, 255.0]),
            np.array([255.0, 190.0, 80.0]),
            np.array([100.0, 240.0, 175.0]),
        ]
        sheet = Image.new("RGB", (w * 2, h * 2), (20, 16, 24))
        for i in range(4):
            m = rgba_u8[:, :, i].astype(np.float32) / 255.0
            vis = arr * (1.0 - 0.58 * m[:, :, None]) + colors[i][None, None, :] * (0.58 * m[:, :, None])
            r_idx, c_idx = divmod(i, 2)
            sheet.paste(Image.fromarray(np.clip(vis, 0, 255).astype(np.uint8)), (c_idx * w, r_idx * h))
        audit_path = Path(audit_dir) / "c01_mask_audit.png"
        sheet.save(audit_path)
        print(f"Saved audit sheet: {audit_path}")


if __name__ == "__main__":
    main()

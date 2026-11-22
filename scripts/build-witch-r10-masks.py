#!/usr/bin/env python3
"""Generate pixel-locked RGBA mask texture for R10 (The Mirror Self) animated card art.

Never modifies the original artwork (SpireLab/SpireLab/images/witch/cards/r10.png).
Channels in r10_masks.png (500x380):
  R: Mirror glass interior glare mask (excludes frame, real Witch, and attenuates over face skin)
  G: Mirror Self crimson iris mask (pixel-locked to crimson eye pixels only)
  B: Existing painted glass crack mask (extracted along the real lower-right crack lines)
  A: Feathered inner mirror breathing/parallax mask (strictly 0 at frame & touching hand)
"""

from __future__ import annotations

import argparse
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFilter


def polygon_mask(size: tuple[int, int], points: list[tuple[float, float]], blur: float = 0.0) -> np.ndarray:
    img = Image.new("L", size, 0)
    ImageDraw.Draw(img).polygon(points, fill=255)
    if blur > 0:
        img = img.filter(ImageFilter.GaussianBlur(radius=blur))
    return np.asarray(img, dtype=np.float32) / 255.0


def polyline_mask(size: tuple[int, int], polylines: list[list[tuple[float, float]]], width: int, blur: float = 0.0) -> np.ndarray:
    img = Image.new("L", size, 0)
    draw = ImageDraw.Draw(img)
    for line in polylines:
        draw.line(line, fill=255, width=width, joint="curve")
    if blur > 0:
        img = img.filter(ImageFilter.GaussianBlur(radius=blur))
    return np.asarray(img, dtype=np.float32) / 255.0


def build_masks(src_path: Path, out_path: Path, audit_path: Path | None = None) -> None:
    im = Image.open(src_path).convert("RGBA")
    w, h = im.size
    if (w, h) != (500, 380):
        raise ValueError(f"Expected 500x380 for r10.png, got {w}x{h}")

    arr = np.asarray(im, dtype=np.float32)
    r, g, b = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2]
    lum = 0.299 * r + 0.587 * g + 0.114 * b

    # 1. Mirror glass interior polygon (verified against r10_grid.png):
    # Strictly inside the ornate silver mirror frame and right of the real Witch's hat brim & touching fingers.
    glass_poly = [
        (308, 14), (344, 6), (388, 5), (428, 16), (456, 46),
        (472, 92), (470, 142), (458, 196), (436, 250), (404, 298),
        (368, 338), (336, 362), (308, 366), (282, 354),
        (277, 326), (275, 260), (272, 200), (264, 180),
        (248, 152), (249, 112), (262, 74), (282, 38),
    ]
    glass_raw = polygon_mask((w, h), glass_poly, blur=3.0)

    # Protect Mirror Self's pale face/eye/smile from bright glare washout.
    face_protect_poly = [
        (284, 128), (318, 114), (350, 124), (356, 156),
        (344, 190), (312, 206), (286, 184), (278, 156),
    ]
    face_protect = polygon_mask((w, h), face_protect_poly, blur=9.0)
    channel_r = np.clip(glass_raw * (1.0 - 0.82 * face_protect), 0.0, 1.0)

    # 2. Mirror Self's crimson iris ONLY (verified on 4x crop: x in [308..325], y in [130..148]).
    eye_roi = np.zeros((h, w), dtype=np.float32)
    eye_roi[130:149, 308:326] = 1.0
    crimson_score = np.clip((r - np.maximum(g, b) - 38.0) / 70.0, 0.0, 1.0) * np.clip((r - 95.0) / 80.0, 0.0, 1.0)
    eye_raw = crimson_score * eye_roi
    # Gently soften 1px so highlight blends smoothly without hard stair-stepping.
    eye_img = Image.fromarray((eye_raw * 255.0).astype(np.uint8)).filter(ImageFilter.GaussianBlur(radius=1.0))
    channel_g = np.clip(np.asarray(eye_img, dtype=np.float32) / 255.0 * 1.35, 0.0, 1.0)
    # Keep strictly confined within a tight safety window around the eye.
    eye_clamp = polygon_mask((w, h), [(306, 130), (326, 128), (326, 148), (306, 148)], blur=1.5)
    channel_g = np.clip(channel_g * eye_clamp, 0.0, 1.0)

    # 3. Existing painted glass cracks along the lower-right mirror glass (verified on r10_grid.png).
    crack_corridor = polyline_mask(
        (w, h),
        [
            [(281, 352), (304, 305), (341, 272), (371, 233), (406, 206), (426, 182), (452, 152), (473, 129)],
            [(409, 204), (444, 195), (469, 187)],
            [(423, 184), (435, 156)],
            [(339, 274), (364, 256), (384, 238)],
        ],
        width=10,
        blur=2.5,
    )
    # Lock onto actual cool-bright crack pixels inside the corridor (high luminance, not warm skin/lace).
    coolness = np.clip((b - r + 32.0) / 52.0, 0.0, 1.0)
    bright_crack = np.clip((lum - 132.0) / 95.0, 0.0, 1.0)
    crack_pixels = crack_corridor * glass_raw * bright_crack * (0.35 + 0.65 * coolness)
    crack_img = Image.fromarray((np.clip(crack_pixels * 1.45, 0.0, 1.0) * 255.0).astype(np.uint8))
    crack_img = crack_img.filter(ImageFilter.GaussianBlur(radius=0.8))
    channel_b = np.clip(np.asarray(crack_img, dtype=np.float32) / 255.0, 0.0, 1.0)

    # 4. Safe feathered inner mirror breathing/parallax mask:
    # Stays well inside the silver frame, right of the real hand, AND above the lower-right crack plane
    # so the glass crack stays locked to the mirror surface while only the reflection gently breathes.
    breath_poly = [
        (320, 44), (364, 34), (408, 48), (432, 86), (428, 134),
        (396, 178), (356, 222), (316, 260), (294, 250),
        (288, 196), (278, 148), (282, 102), (298, 66),
    ]
    channel_a = polygon_mask((w, h), breath_poly, blur=14.0) * glass_raw * (1.0 - np.clip(crack_corridor * 1.2, 0.0, 1.0))

    rgba = np.stack(
        [
            (np.clip(channel_r, 0.0, 1.0) * 255.0).round().astype(np.uint8),
            (np.clip(channel_g, 0.0, 1.0) * 255.0).round().astype(np.uint8),
            (np.clip(channel_b, 0.0, 1.0) * 255.0).round().astype(np.uint8),
            (np.clip(channel_a, 0.0, 1.0) * 255.0).round().astype(np.uint8),
        ],
        axis=-1,
    )
    out_path.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(rgba).save(out_path, format="PNG")
    print(f"Wrote {out_path} ({out_path.stat().st_size} bytes)")
    print(
        f"Coverage: R(glass)={channel_r.mean():.3f}, G(eye_px>0.2)={(channel_g > 0.2).sum()}, "
        f"B(crack_px>0.2)={(channel_b > 0.2).sum()}, A(breath)={channel_a.mean():.3f}"
    )

    if audit_path is not None:
        # Create a 2x2 visual audit sheet overlaying each mask channel on r10.png.
        base = arr[:, :, :3]
        panels = [
            ("R: Glass Sweep (face protected)", channel_r, np.array([170, 215, 255], dtype=np.float32)),
            ("G: Mirror Self Iris", channel_g, np.array([255, 45, 95], dtype=np.float32)),
            ("B: Existing Glass Cracks", channel_b, np.array([120, 245, 255], dtype=np.float32)),
            ("A: Safe Inner Breath Zone", channel_a, np.array([210, 120, 255], dtype=np.float32)),
        ]
        sheet = Image.new("RGB", (w * 2, h * 2))
        for idx, (label, mask, tint) in enumerate(panels):
            alpha = mask[:, :, None] * 0.72
            blended = np.clip(base * (1.0 - alpha * 0.45) + tint * alpha, 0, 255).astype(np.uint8)
            panel = Image.fromarray(blended)
            draw = ImageDraw.Draw(panel)
            draw.rectangle([(4, 4), (255, 22)], fill=(16, 12, 18))
            draw.text((8, 7), label, fill=(245, 235, 240))
            sheet.paste(panel, ((idx % 2) * w, (idx // 2) * h))
        audit_path.parent.mkdir(parents=True, exist_ok=True)
        sheet.save(audit_path, format="PNG")
        print(f"Wrote visual audit: {audit_path}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--src", type=Path, default=Path("SpireLab/SpireLab/images/witch/cards/r10.png"))
    parser.add_argument("--out", type=Path, default=Path("SpireLab/SpireLab/images/witch/cards/animated/r10/r10_masks.png"))
    parser.add_argument("--audit", type=Path, default=None)
    args = parser.parse_args()
    build_masks(args.src, args.out, args.audit)


if __name__ == "__main__":
    main()

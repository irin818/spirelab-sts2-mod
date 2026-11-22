#!/usr/bin/env python3
"""Reproducibly generate pixel-locked Live2D-style rig weight masks for the Witch combat sprites.

Never modifies original art (`witch_sprite.png`, `witch_mirror_form.png`).
Outputs:
  - SpireLab/SpireLab/images/witch/live/witch_rig_masks.png (391x438 RGBA)
      R: Head + wide-brim hat rigid pivot weight (pivots at neck; zero face distortion)
      G: Secondary physics sway weight (flowing back hair curls, bent hat tip + ruby pendant, lower ribbons/crosses)
      B: Layered gothic skirt & ivory lace hem cloth-wave weight (strictly zero on legs & boots)
      A: Crimson iris + ruby gems glow mask (eyes, hat ruby charm, hat brooch gem, grimoire gem)
  - SpireLab/SpireLab/images/witch/live/mirror_rig_masks.png (238x438 RGBA)
      R: Inner oval mirror glass area (strictly excluding ornate silver frame)
      G: Mirror Self crimson eyes glow mask
      B: Existing red glass cracks + frame ruby gems mask
      A: Feathered inner Mirror Self body breath/sway mask (zero on silver frame)
"""

from __future__ import annotations

import os
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
WITCH_SPRITE = ROOT / "SpireLab/SpireLab/images/witch/witch_sprite.png"
MIRROR_SPRITE = ROOT / "SpireLab/SpireLab/images/witch/witch_mirror_form.png"
OUT_DIR = ROOT / "SpireLab/SpireLab/images/witch/live"


def smoothstep(edge0: float, edge1: float, x: np.ndarray) -> np.ndarray:
    t = np.clip((x - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def blur_f(arr: np.ndarray, radius: float) -> np.ndarray:
    im = Image.fromarray(np.clip(arr * 255.0, 0, 255).astype(np.uint8), mode="L")
    im = im.filter(ImageFilter.GaussianBlur(radius=radius))
    return np.asarray(im, dtype=np.float32) / 255.0


def build_witch_masks() -> np.ndarray:
    im = Image.open(WITCH_SPRITE).convert("RGBA")
    arr = np.asarray(im, dtype=np.float32)
    h, w = arr.shape[:2]
    assert (w, h) == (391, 438), f"Unexpected witch_sprite.png size: {w}x{h}"

    r, g, b, a = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2], arr[:, :, 3]
    alpha = (a > 8.0).astype(np.float32)

    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)

    # --- R channel: Head + wide-brim hat rigid pivot weight ---
    # Neck sits around x=212, y=122. Everything above the collar/neck pivots cleanly together.
    # Gently taper across neck y=[112..132] and exclude left trailing back-hair curls below hat brim.
    neck_band = 1.0 - smoothstep(110.0, 134.0, yy)
    # Left hat brim extends down to y~126 at x~120..175; back hair below hat brim starts around y>120 for x<190
    brim_line = 128.0 - 0.16 * np.clip(xx - 120.0, 0.0, 120.0)
    above_brim = 1.0 - smoothstep(brim_line - 6.0, brim_line + 14.0, yy)
    head_zone = np.where(xx < 194.0, above_brim, neck_band)
    head_mask = blur_f(head_zone, 4.0) * alpha

    # --- G channel: Secondary physics sway (back hair curls + bent hat tip/pendant + lower hanging crosses/ribbons) ---
    # 1) Flowing back hair mass on the left (x=[102..195], y=[118..290]), smoothly tapered away from torso/shoulder (x>182)
    hair_x = 1.0 - smoothstep(166.0, 198.0, xx)
    hair_y = smoothstep(114.0, 136.0, yy) * (1.0 - smoothstep(260.0, 298.0, yy))
    # Stronger sway further left/down toward the curly hair tips
    hair_amp = 0.45 + 0.55 * (1.0 - smoothstep(106.0, 185.0, xx))
    hair_zone = hair_x * hair_y * hair_amp

    # 2) Bent witch-hat tip & dangling crimson ruby pendant on top-left (x=[122..172], y=[18..92])
    hat_tip_x = 1.0 - smoothstep(154.0, 178.0, xx)
    hat_tip_y = smoothstep(16.0, 30.0, yy) * (1.0 - smoothstep(82.0, 102.0, yy))
    hat_tip_zone = hat_tip_x * hat_tip_y * 0.92

    # 3) Hanging lower skirt tails & dangling silver cross pendants (y=[288..348], excluding legs x=[184..234])
    leg_protect = 1.0 - (smoothstep(178.0, 192.0, xx) * (1.0 - smoothstep(226.0, 240.0, xx)))
    pendant_zone = smoothstep(284.0, 306.0, yy) * (1.0 - smoothstep(344.0, 362.0, yy)) * leg_protect * 0.85

    secondary_mask = np.clip(np.maximum.reduce([hair_zone, hat_tip_zone, pendant_zone]), 0.0, 1.0)
    secondary_mask = blur_f(secondary_mask, 3.5) * alpha

    # --- B channel: Layered gothic skirt & ivory lace hem cloth-wave weight ---
    # Anchored 0 at waist (y=192), rises cleanly across the flared skirt (y=[200..342]),
    # strictly excluding legs & boots (y>295 inside x=[180..236]) and excluded on boots (y>350).
    skirt_y = smoothstep(190.0, 232.0, yy) * (1.0 - smoothstep(336.0, 356.0, yy))
    # Exclude the upper back hair curls on far left above skirt hem (y < 235, x < 165)
    skirt_left_guard = np.where(yy < 240.0, smoothstep(148.0, 178.0, xx), 1.0)
    # Protect exposed legs below skirt ruffle line (y > 286, x in [186..234])
    legs_inner = smoothstep(182.0, 196.0, xx) * (1.0 - smoothstep(224.0, 238.0, xx)) * smoothstep(284.0, 300.0, yy)
    skirt_zone = skirt_y * skirt_left_guard * (1.0 - legs_inner)
    # Increase cloth amplitude gradually toward outer hem
    hem_ramp = 0.35 + 0.65 * smoothstep(210.0, 315.0, yy)
    skirt_mask = blur_f(skirt_zone * hem_ramp, 3.5) * alpha

    # --- A channel: Crimson iris + ruby gems glow mask ---
    crimson = (a > 120.0) & (r > 120.0) & (g < 95.0) & ((r - g) > 58.0) & ((r - b) > 32.0)
    # Restrict strictly to authentic glowing features (eyes, hat ruby charm, hat brooch gem, grimoire gem)
    gem_boxes = np.zeros((h, w), dtype=bool)
    gem_boxes[64:82, 132:145] = True   # hat ruby pendant
    gem_boxes[73:93, 173:188] = True   # hat brooch ruby
    gem_boxes[89:106, 203:236] = True  # both crimson irises
    gem_boxes[152:168, 245:257] = True # grimoire ruby gem only (excludes hand)
    glow_raw = (crimson & gem_boxes).astype(np.float32)
    glow_mask = np.clip(blur_f(glow_raw, 1.3) * 1.45, 0.0, 1.0) * alpha

    out = np.stack([head_mask, secondary_mask, skirt_mask, glow_mask], axis=-1)
    return np.clip(np.round(out * 255.0), 0, 255).astype(np.uint8)


def build_mirror_masks() -> np.ndarray:
    im = Image.open(MIRROR_SPRITE).convert("RGBA")
    arr = np.asarray(im, dtype=np.float32)
    h, w = arr.shape[:2]
    assert (w, h) == (238, 438), f"Unexpected witch_mirror_form.png size: {w}x{h}"

    r, g, b, a = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2], arr[:, :, 3]
    alpha = (a > 8.0).astype(np.float32)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)

    # Oval mirror glass sits cleanly centered at (cx=119, cy=255), rx=53, ry=114
    cx, cy, rx, ry = 119.0, 255.0, 52.5, 113.5
    oval_d = np.sqrt(((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2)

    # R: Inner oval mirror glass (excluding ornate silver outer frame)
    glass_mask = (1.0 - smoothstep(0.92, 1.01, oval_d)) * alpha

    # G: Mirror Self crimson eyes only (strictly excludes laughing mouth below y=214)
    crimson = (a > 120.0) & (r > 105.0) & (g < 88.0) & ((r - g) > 58.0) & ((r - b) > 28.0)
    eye_box = np.zeros((h, w), dtype=bool)
    eye_box[197:214, 118:156] = True
    eye_raw = (crimson & eye_box).astype(np.float32)
    eye_mask = np.clip(blur_f(eye_raw, 1.2) * 1.4, 0.0, 1.0) * glass_mask

    # B: Existing red cobweb cracks inside glass + ornate frame ruby gems (excludes skin/hand/mouth)
    frame_gem_box = np.zeros((h, w), dtype=bool)
    frame_gem_box[116:135, 112:127] = True
    frame_gem_box[364:418, 110:130] = True
    frame_gem_box[236:260, 47:62] = True
    frame_gem_box[236:260, 176:191] = True
    frame_gems = (crimson & frame_gem_box).astype(np.float32)

    # Red cracks inside glass: high-red dark-green pixels outside face/hand skin region
    face_mouth_hand = np.zeros((h, w), dtype=bool)
    face_mouth_hand[196:235, 116:162] = True  # face/mouth
    face_mouth_hand[260:328, 72:124] = True   # reaching hand
    face_mouth_hand[212:255, 148:172] = True  # raised right hand
    red_cracks = (crimson & (~face_mouth_hand) & (oval_d < 0.96)).astype(np.float32)
    cracks_and_gems = np.clip(blur_f(np.maximum(red_cracks, frame_gems), 0.9) * 1.35, 0.0, 1.0) * alpha

    # A: Deeply feathered inner Mirror Self body weight (for smooth asynchronous Live2D breathing inside glass)
    inner_breath = (1.0 - smoothstep(0.60, 0.93, oval_d)) * alpha
    inner_breath = blur_f(inner_breath, 4.0) * alpha

    out = np.stack([glass_mask, eye_mask, cracks_and_gems, inner_breath], axis=-1)
    return np.clip(np.round(out * 255.0), 0, 255).astype(np.uint8)


def save_audit(sprite_path: Path, mask_rgba: np.ndarray, out_path: Path, labels: list[str]) -> None:
    base = np.asarray(Image.open(sprite_path).convert("RGBA"), dtype=np.float32)
    h, w = base.shape[:2]
    colors = [
        np.array([255.0, 80.0, 110.0]),   # R: pink-red
        np.array([80.0, 235.0, 170.0]),   # G: mint
        np.array([90.0, 170.0, 255.0]),   # B: ice-blue
        np.array([255.0, 210.0, 80.0]),   # A: gold
    ]
    sheet = Image.new("RGB", (w * 4, h), (22, 18, 26))
    for i in range(4):
        m = mask_rgba[:, :, i].astype(np.float32) / 255.0
        bg = np.full((h, w, 3), [22.0, 18.0, 26.0], dtype=np.float32)
        sa = base[:, :, 3:4] / 255.0
        comp = base[:, :, :3] * sa + bg * (1.0 - sa)
        vis = comp * (1.0 - 0.58 * m[:, :, None]) + colors[i][None, None, :] * (0.58 * m[:, :, None])
        sheet.paste(Image.fromarray(np.clip(vis, 0, 255).astype(np.uint8)), (i * w, 0))
    out_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out_path)


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    witch_mask = build_witch_masks()
    mirror_mask = build_mirror_masks()

    witch_out = OUT_DIR / "witch_rig_masks.png"
    mirror_out = OUT_DIR / "mirror_rig_masks.png"
    Image.fromarray(witch_mask, mode="RGBA").save(witch_out, optimize=True)
    Image.fromarray(mirror_mask, mode="RGBA").save(mirror_out, optimize=True)

    print(f"Wrote {witch_out} ({witch_out.stat().st_size} bytes)")
    for idx, ch in enumerate(["R(head)", "G(hair/hat/pendant)", "B(skirt)", "A(eyes/gems)"]):
        px = int((witch_mask[:, :, idx] > 20).sum())
        print(f"  witch {ch}: {px} px > 20, max={witch_mask[:, :, idx].max()}")

    print(f"Wrote {mirror_out} ({mirror_out.stat().st_size} bytes)")
    for idx, ch in enumerate(["R(glass)", "G(eyes)", "B(cracks/gems)", "A(breath)"]):
        px = int((mirror_mask[:, :, idx] > 20).sum())
        print(f"  mirror {ch}: {px} px > 20, max={mirror_mask[:, :, idx].max()}")

    audit_dir = os.environ.get("WITCH_MASK_AUDIT_DIR")
    if audit_dir:
        ad = Path(audit_dir)
        save_audit(WITCH_SPRITE, witch_mask, ad / "witch_rig_mask_audit.png", ["R", "G", "B", "A"])
        save_audit(MIRROR_SPRITE, mirror_mask, ad / "mirror_rig_mask_audit.png", ["R", "G", "B", "A"])
        print(f"Saved audit sheets in {ad}")


if __name__ == "__main__":
    main()

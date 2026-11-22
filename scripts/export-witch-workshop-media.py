#!/usr/bin/env python3
"""Render Steam Workshop cover art, 10s trailer, optional 1:1 GIF, and page adaptation sheets."""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import shlex
import shutil
import subprocess

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont, ImageOps


FONT_DIR = Path("/System/Library/Fonts/Supplemental")
ROOT = Path(__file__).resolve().parents[1]
ART_DIR = ROOT / "SpireLab/SpireLab/images/witch/finisher"
AUDIO_DIR = ROOT / "SpireLab/SpireLab/audio/witch/finisher"
DEFAULT_GAMEPLAY = ROOT / ".local/crimson-v4/evidence/crimson-final-mirror-v4.mp4"

FPS = 60
# Frame-accurate beats from the recorded V4 first kill (shatter explosion at 2.82s).
# 0.0-1.5s: animated gothic eyes/title intro
# 1.5-3.0s: lethal hit, enemy lock-on, camera pull-back, mirror drape opens (0.14s -> 1.43s)
# 3.0-5.0s: Witch materializes behind mirror, looks up, tilts head, wicked smile (1.43s -> 2.35s)
# 5.0-7.0s: reaching hand, mirror pulls, magic converges, portrait dissolves (2.35s -> 2.81s)
# 7.0-8.5s: fist clench + mirror shatter climax at 7.05s, shards scatter (2.81s -> 3.46s)
# 8.5-10.0s: clean gothic shard dissipation + final THE WITCH / CRIMSON FINAL MIRROR outro
SHOTS = [
    (0.14, 1.43, 1.50),
    (1.43, 2.35, 2.00),
    (2.35, 2.81, 2.00),
    (2.81, 3.46, 1.50),
]

IVORY_TOP = (255, 246, 236)
IVORY_BOT = (224, 198, 214)
ROSE_TOP = (248, 174, 192)
ROSE_BOT = (206, 86, 118)
CRIMSON_GLOW = (204, 30, 68)
DEEP_SHADOW = (11, 5, 16)


def resolve_tool(name):
    found = shutil.which(name)
    if found:
        return found
    for prefix in ("/opt/homebrew/bin", "/usr/local/bin"):
        candidate = Path(prefix) / name
        if candidate.is_file() and os.access(candidate, os.X_OK):
            return str(candidate)
    raise FileNotFoundError(f"Required tool not found: {name}")


def font(size, *, style="display"):
    if style == "chinese":
        return ImageFont.truetype(str(FONT_DIR / "Songti.ttc"), size, index=1)
    if style == "sub":
        return ImageFont.truetype(str(FONT_DIR / "Baskerville.ttc"), size, index=4)
    return ImageFont.truetype(str(FONT_DIR / "Bodoni 72.ttc"), size, index=2)


def smoothstep(edge0, edge1, x):
    t = np.clip((x - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def quintic(t):
    t = max(0.0, min(1.0, t))
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0)


def load_rgba(rel_path):
    return Image.open(ART_DIR / rel_path).convert("RGBA")


def build_witch_portrait(smile=1.0, eyes_glow=0.95, acting_y=1.0):
    """Fuse v3/portrait.webp and v3/expression.webp using portrait.gdshader registration & edge feathering."""
    base = load_rgba("v3/portrait.webp")
    expr = load_rgba("v3/expression.webp")
    w, h = base.size
    arr = np.asarray(base, dtype=np.float32) / 255.0
    earr = np.asarray(expr, dtype=np.float32) / 255.0
    eh, ew = earr.shape[:2]

    xs = (np.arange(w, dtype=np.float32) + 0.5) / w
    ys = (np.arange(h, dtype=np.float32) + 0.5) / h
    gx, gy = np.meshgrid(xs, ys)

    def fill_oval(cx, cy, rx, ry):
        d2 = ((gx - cx) / rx) ** 2 + ((gy - cy) / ry) ** 2
        return 1.0 - smoothstep(0.30, 1.0, d2)

    eye_l = (0.571, 0.328)
    eye_r = (0.676, 0.260)
    mouth = (0.656, 0.389)
    face_l = fill_oval(eye_l[0], eye_l[1], 0.054, 0.044)
    face_r = fill_oval(eye_r[0], eye_r[1], 0.060, 0.043)
    eye_matte = np.maximum(face_l, face_r)
    brow = fill_oval(eye_r[0] - 0.002, eye_r[1] - 0.033, 0.045, 0.028)
    lip_matte = fill_oval(mouth[0], mouth[1], 0.068, 0.078)
    perf = np.maximum(np.maximum(eye_matte * acting_y, brow * acting_y), lip_matte * smile)

    t_x = smoothstep(0.60, 0.65, gx)
    reg_x = -0.002 * (1.0 - t_x) + (-0.003) * t_x
    reg_y = -0.002 * (1.0 - t_x) + (-0.001) * t_x
    lip_t = smoothstep(0.344, 0.387, gy) * lip_matte
    reg_x = reg_x * (1.0 - lip_t) + (-0.007) * lip_t
    reg_y = reg_y * (1.0 - lip_t) + (-0.010) * lip_t

    box_x, box_y, box_w, box_h = 0.501196, 0.162593, 0.260167, 0.326249
    ku = np.clip((gx + reg_x - box_x) / box_w, 0.002, 0.998) * (ew - 1)
    kv = np.clip((gy + reg_y - box_y) / box_h, 0.002, 0.998) * (eh - 1)

    x0 = np.floor(ku).astype(np.int32)
    y0 = np.floor(kv).astype(np.int32)
    x1 = np.minimum(x0 + 1, ew - 1)
    y1 = np.minimum(y0 + 1, eh - 1)
    fx = (ku - x0)[..., None]
    fy = (kv - y0)[..., None]
    sampled = (
        earr[y0, x0] * (1.0 - fx) * (1.0 - fy)
        + earr[y0, x1] * fx * (1.0 - fy)
        + earr[y1, x0] * (1.0 - fx) * fy
        + earr[y1, x1] * fx * fy
    )

    blend = (perf * 0.96)[..., None]
    rgb = arr[..., :3] * (1.0 - blend) + sampled[..., :3] * blend

    # Crimson iris glow from portrait.gdshader (enhanced cleanly for thumbnail legibility).
    il_x = (gx - eye_l[0] - 0.006) * 92.0
    il_y = (gy - eye_l[1] - 0.001) * 154.0
    ir_x = (gx - eye_r[0] - 0.002) * 92.0
    ir_y = (gy - eye_r[1] - 0.001) * 154.0
    glow = np.exp(-(il_x * il_x + il_y * il_y)) + np.exp(-(ir_x * ir_x + ir_y * ir_y))
    rgb = rgb + np.array([0.74, 0.055, 0.14], dtype=np.float32) * (eyes_glow * glow)[..., None]

    # Soften top crop edge (matching portrait.gdshader frameEdge) and bottom shoulder veil so no hard seams appear.
    top_edge = smoothstep(0.0, 0.075, gy)
    right_edge = 1.0 - smoothstep(0.975, 1.0, gx)
    veil = smoothstep(0.76, 0.995, gy)[..., None]
    rgb = rgb * (1.0 - veil * 0.48) + (rgb * np.array([0.50, 0.32, 0.66], dtype=np.float32)) * (veil * 0.48)
    alpha = arr[..., 3] * top_edge * right_edge * (1.0 - smoothstep(0.84, 0.995, gy))

    out = np.concatenate([np.clip(rgb, 0.0, 1.0), np.clip(alpha, 0.0, 1.0)[..., None]], axis=-1)
    return Image.fromarray((out * 255.0).round().astype(np.uint8))


def feather_sleeve(hand_img):
    """Apply the sleeve.gdshader bottom cuff feathering so the reaching arm merges seamlessly."""
    arr = np.asarray(hand_img.convert("RGBA"), dtype=np.float32) / 255.0
    h, w = arr.shape[:2]
    xs = (np.arange(w, dtype=np.float32) + 0.5) / w
    ys = (np.arange(h, dtype=np.float32) + 0.5) / h
    gx, gy = np.meshgrid(xs, ys)
    bottom_fade = 1.0 - smoothstep(0.82, 0.995, gy)
    right_fade = 1.0 - smoothstep(0.86, 0.995, gx) * smoothstep(0.55, 0.85, gy)
    arr[..., 3] *= bottom_fade * right_fade
    return Image.fromarray((np.clip(arr, 0.0, 1.0) * 255.0).round().astype(np.uint8))


def radial_glow(size, center, radius_x, radius_y, color, alpha=1.0, power=2.0):
    w, h = size
    xs = np.arange(w, dtype=np.float32)
    ys = np.arange(h, dtype=np.float32)
    gx, gy = np.meshgrid(xs, ys)
    d = np.sqrt(((gx - center[0]) / radius_x) ** 2 + ((gy - center[1]) / radius_y) ** 2)
    mask = np.clip(1.0 - d, 0.0, 1.0) ** power * alpha
    arr = np.zeros((h, w, 4), dtype=np.uint8)
    arr[..., 0] = color[0]
    arr[..., 1] = color[1]
    arr[..., 2] = color[2]
    arr[..., 3] = (mask * 255.0).round().astype(np.uint8)
    return Image.fromarray(arr)


def draw_magic_filaments(canvas, start, end, *, color_core=(255, 200, 218), color_glow=(228, 36, 86),
                         count=5, spread=85.0, width=3.8, phase=0.0):
    w, h = canvas.size
    glow_layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    core_layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    dg = ImageDraw.Draw(glow_layer)
    dc = ImageDraw.Draw(core_layer)
    sx, sy = start
    ex, ey = end
    dx, dy = ex - sx, ey - sy
    length = math.hypot(dx, dy) or 1.0
    nx, ny = -dy / length, dx / length
    for i in range(count):
        k = (i - (count - 1) / 2.0) / max(1.0, (count - 1) / 2.0)
        pts = []
        for step in range(28):
            t = step / 27.0
            bow = math.sin(t * math.pi) * (k * spread + math.sin(t * 7.0 + i * 1.4 + phase) * 14.0)
            px = sx + dx * t + nx * bow
            py = sy + dy * t + ny * bow
            pts.append((px, py))
        dg.line(pts, fill=(*color_glow, int(175 - abs(k) * 55)), width=max(2, int(width * 2.8)), joint="curve")
        dc.line(pts, fill=(*color_core, int(220 - abs(k) * 70)), width=max(1, int(width)), joint="curve")
    glow_layer = glow_layer.filter(ImageFilter.GaussianBlur(radius=9))
    core_layer = core_layer.filter(ImageFilter.GaussianBlur(radius=1.1))
    canvas.alpha_composite(glow_layer)
    canvas.alpha_composite(core_layer)


def draw_crimson_embers(canvas, center, *, seed=19, count=36, radius_min=190, radius_max=720, phase=0.0, scale=1.0):
    """Paint atmospheric luminous crimson embers & fine starlight shards strictly outside the face/title zones."""
    w, h = canvas.size
    glow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    core = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    dg = ImageDraw.Draw(glow)
    dc = ImageDraw.Draw(core)
    cx, cy = center
    rng = np.random.default_rng(seed)
    for i in range(count):
        ang = rng.uniform(0, math.tau) + phase * (0.04 if i % 2 == 0 else -0.03)
        dist = rng.uniform(radius_min, radius_max) * scale
        px = cx + math.cos(ang) * dist
        py = cy + math.sin(ang) * dist * 0.88 - (phase * 10.0 * scale if i % 2 == 0 else 0.0)
        # Strictly protect Witch face, neck, earrings, and title area.
        if (abs(px - w * 0.50) < w * 0.25 and h * 0.14 < py < h * 0.54):
            continue
        if py > h * 0.75 and abs(px - w * 0.50) < w * 0.40:
            continue
        r = rng.uniform(2.5, 7.5) * scale
        rot = rng.uniform(0, math.tau)
        # Delicate elongated gothic diamond spark.
        pts = [
            (px + r * 2.1 * math.cos(rot), py + r * 2.1 * math.sin(rot)),
            (px - r * 0.65 * math.sin(rot), py + r * 0.65 * math.cos(rot)),
            (px - r * 2.1 * math.cos(rot), py - r * 2.1 * math.sin(rot)),
            (px + r * 0.65 * math.sin(rot), py - r * 0.65 * math.cos(rot)),
        ]
        twinkle = 0.65 + 0.35 * math.sin(phase * 2.0 + i * 1.1)
        if i % 3 == 0:
            dg.ellipse((px - r * 2.6, py - r * 2.6, px + r * 2.6, py + r * 2.6),
                       fill=(235, 34, 82, int(135 * twinkle)))
            dc.polygon(pts, fill=(255, 208, 224, int(230 * twinkle)))
        else:
            dg.ellipse((px - r * 2.2, py - r * 2.2, px + r * 2.2, py + r * 2.2),
                       fill=(196, 138, 224, int(105 * twinkle)))
            dc.polygon(pts, fill=(246, 232, 255, int(215 * twinkle)))
    glow = glow.filter(ImageFilter.GaussianBlur(radius=max(3.0, 6.0 * scale)))
    core = core.filter(ImageFilter.GaussianBlur(radius=0.6))
    canvas.alpha_composite(glow)
    canvas.alpha_composite(core)


def draw_mirror_cracks(canvas, center, radius, *, alpha=195):
    w, h = canvas.size
    layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    glow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    dl = ImageDraw.Draw(layer)
    dg = ImageDraw.Draw(glow)
    cx, cy = center
    paths = [
        [(-0.78, -0.52), (-0.36, -0.28), (-0.18, -0.06), (0.0, 0.0), (0.14, 0.32), (0.44, 0.42), (0.64, 0.78)],
        [(-0.50, 0.82), (-0.34, 0.30), (-0.08, 0.12), (0.0, 0.0), (0.28, -0.22), (0.22, -0.52), (0.50, -0.88)],
        [(-0.80, 0.04), (-0.48, 0.12), (0.0, 0.0), (0.38, -0.10), (0.58, -0.34), (0.82, -0.44)],
    ]
    for idx, pts in enumerate(paths):
        abs_pts = [(cx + x * radius, cy + y * radius) for x, y in pts]
        dg.line(abs_pts, fill=(235, 36, 84, int(alpha * 0.72)), width=6, joint="curve")
        color = (255, 206, 224, alpha) if idx == 1 else (242, 92, 132, alpha)
        dl.line(abs_pts, fill=color, width=2, joint="curve")
    glow = glow.filter(ImageFilter.GaussianBlur(radius=5))
    canvas.alpha_composite(glow)
    canvas.alpha_composite(layer)


def paste_centered(canvas, sprite, center, *, scale=1.0, rotate=0.0, flip_x=False, opacity=1.0, tint=None):
    im = sprite.copy()
    if flip_x:
        im = ImageOps.mirror(im)
    if scale != 1.0:
        im = im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))), Image.Resampling.LANCZOS)
    if rotate != 0.0:
        im = im.rotate(rotate, resample=Image.Resampling.BICUBIC, expand=True)
    if tint is not None or opacity < 1.0:
        arr = np.asarray(im, dtype=np.float32) / 255.0
        if tint is not None:
            arr[..., :3] *= np.array(tint, dtype=np.float32)
        arr[..., 3] *= opacity
        im = Image.fromarray((np.clip(arr, 0.0, 1.0) * 255.0).round().astype(np.uint8))
    x = round(center[0] - im.width / 2)
    y = round(center[1] - im.height / 2)
    canvas.alpha_composite(im, (x, y))


def render_gradient_text(canvas, text, center_x, center_y, size, tracking, *,
                         style="display", top_color=IVORY_TOP, bot_color=IVORY_BOT,
                         glow_color=CRIMSON_GLOW, glow_alpha=0.55, max_width=None, shadow_offset=(0, 6)):
    """Render tracked serif text with metallic vertical gradient, velvet shadow, and subtle crimson aura."""
    face = font(size, style=style)
    tmp = ImageDraw.Draw(Image.new("RGBA", (10, 10)))
    widths = [tmp.textlength(ch, font=face) for ch in text]
    total_w = sum(widths) + tracking * max(0, len(text) - 1)
    if max_width and total_w > max_width:
        scale_down = max_width / total_w
        return render_gradient_text(
            canvas, text, center_x, center_y, max(12, int(size * scale_down)),
            max(1, round(tracking * scale_down)),
            style=style, top_color=top_color, bot_color=bot_color,
            glow_color=glow_color, glow_alpha=glow_alpha, max_width=None, shadow_offset=shadow_offset,
        )

    pad = max(36, int(size * 0.45))
    box_w = int(math.ceil(total_w)) + pad * 2
    box_h = int(math.ceil(size * 1.65)) + pad * 2
    mask = Image.new("L", (box_w, box_h), 0)
    dm = ImageDraw.Draw(mask)
    x = pad
    y_mid = box_h // 2
    for ch, cw in zip(text, widths):
        dm.text((x, y_mid), ch, font=face, anchor="lm", fill=255)
        x += cw + tracking

    bbox = mask.getbbox() or (pad, y_mid - size // 2, box_w - pad, y_mid + size // 2)
    y0, y1 = bbox[1], max(bbox[1] + 1, bbox[3])
    grad = np.zeros((box_h, box_w, 4), dtype=np.uint8)
    ys = np.arange(box_h, dtype=np.float32)[:, None]
    t = np.clip((ys - y0) / (y1 - y0), 0.0, 1.0)
    for c in range(3):
        grad[..., c] = (top_color[c] * (1.0 - t) + bot_color[c] * t).round().astype(np.uint8)
    grad[..., 3] = np.asarray(mask, dtype=np.uint8)
    text_img = Image.fromarray(grad)

    shadow_mask = mask.filter(ImageFilter.GaussianBlur(radius=max(3.0, size * 0.045)))
    shadow_arr = np.zeros((box_h, box_w, 4), dtype=np.uint8)
    shadow_arr[..., 0] = 8
    shadow_arr[..., 1] = 3
    shadow_arr[..., 2] = 12
    shadow_arr[..., 3] = np.clip(np.asarray(shadow_mask, dtype=np.float32) * 1.35, 0, 245).astype(np.uint8)
    shadow_img = Image.fromarray(shadow_arr)

    aura_mask = mask.filter(ImageFilter.GaussianBlur(radius=max(6.0, size * 0.095)))
    aura_arr = np.zeros((box_h, box_w, 4), dtype=np.uint8)
    aura_arr[..., 0] = glow_color[0]
    aura_arr[..., 1] = glow_color[1]
    aura_arr[..., 2] = glow_color[2]
    aura_arr[..., 3] = np.clip(np.asarray(aura_mask, dtype=np.float32) * glow_alpha, 0, 220).astype(np.uint8)
    aura_img = Image.fromarray(aura_arr)

    edge_mask = mask.filter(ImageFilter.MaxFilter(3))
    edge_arr = np.zeros((box_h, box_w, 4), dtype=np.uint8)
    edge_arr[..., 0] = 18
    edge_arr[..., 1] = 8
    edge_arr[..., 2] = 24
    edge_arr[..., 3] = np.clip(np.asarray(edge_mask, dtype=np.float32) * 0.72, 0, 210).astype(np.uint8)
    edge_img = Image.fromarray(edge_arr)

    ox = round(center_x - box_w / 2)
    oy = round(center_y - box_h / 2)
    canvas.alpha_composite(aura_img, (ox, oy))
    canvas.alpha_composite(shadow_img, (ox + shadow_offset[0], oy + shadow_offset[1]))
    canvas.alpha_composite(edge_img, (ox, oy))
    canvas.alpha_composite(text_img, (ox, oy))


def draw_gothic_rule(canvas, cx, cy, half_width, *, diamond=11, alpha=235):
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for direction in (-1, 1):
        steps = 40
        for s in range(steps):
            t0 = s / steps
            t1 = (s + 1) / steps
            x0 = cx + direction * (diamond + 12 + t0 * (half_width - diamond - 12))
            x1 = cx + direction * (diamond + 12 + t1 * (half_width - diamond - 12))
            fade = int(alpha * (1.0 - t0) ** 1.35)
            d.line((x0, cy, x1, cy), fill=(212, 178, 202, fade), width=2)
        dot_x = cx + direction * (diamond + 22)
        d.polygon([(dot_x, cy - 4), (dot_x + 4, cy), (dot_x, cy + 4), (dot_x - 4, cy)],
                  fill=(242, 186, 204, alpha))
    d.polygon(
        [(cx, cy - diamond), (cx + diamond * 1.25, cy), (cx, cy + diamond), (cx - diamond * 1.25, cy)],
        fill=(226, 52, 92, alpha),
        outline=(255, 224, 232, alpha),
    )
    d.polygon(
        [(cx, cy - diamond * 0.45), (cx + diamond * 0.55, cy), (cx, cy + diamond * 0.45), (cx - diamond * 0.55, cy)],
        fill=(255, 224, 232, alpha),
    )
    canvas.alpha_composite(layer)


def apply_vignette(canvas, *, top=0.58, bottom=0.92, sides=0.42, bottom_start=0.54):
    w, h = canvas.size
    ys = np.linspace(0.0, 1.0, h, dtype=np.float32)[:, None]
    xs = np.linspace(0.0, 1.0, w, dtype=np.float32)[None, :]
    cov_top = top * np.clip(1.0 - ys / 0.22, 0.0, 1.0) ** 1.6
    cov_bot = bottom * np.clip((ys - bottom_start) / (1.0 - bottom_start), 0.0, 1.0) ** 1.65
    edge = np.maximum(0.0, np.abs(xs - 0.5) * 2.0 - 0.58) / 0.42
    cov_side = sides * (edge ** 1.8)
    cov = np.clip(1.0 - (1.0 - cov_top) * (1.0 - cov_bot) * (1.0 - cov_side), 0.0, 0.96)
    shade = np.zeros((h, w, 4), dtype=np.uint8)
    shade[..., 0] = DEEP_SHADOW[0]
    shade[..., 1] = DEEP_SHADOW[1]
    shade[..., 2] = DEEP_SHADOW[2]
    shade[..., 3] = (cov * 255.0).round().astype(np.uint8)
    canvas.alpha_composite(Image.fromarray(shade))


def compose_square_scene(size=1600, *, smile=1.0, eyes_glow=0.96, hand_reach=0.0, magic_pulse=0.0,
                         phase=0.0, with_text=True, static_bg=False, external_art=None):
    """Compose the 1:1 Crimson Final Mirror key visual from lossless V3/V4 assets."""
    s = size / 1600.0
    if external_art is not None:
        base = ImageOps.fit(Image.open(external_art).convert("RGB"), (size, size), Image.Resampling.LANCZOS).convert("RGBA")
        apply_vignette(base, top=0.72, bottom=0.94, sides=0.25, bottom_start=0.53)
    else:
        base = Image.new("RGBA", (size, size), (*DEEP_SHADOW, 255))
        garden = ImageOps.fit(load_rgba("v4/mirror-garden.webp"), (size, size), Image.Resampling.LANCZOS)
        garden = ImageEnhance.Color(garden).enhance(1.12)
        garden = ImageEnhance.Contrast(garden).enhance(1.08)
        garden = ImageEnhance.Brightness(garden).enhance(0.84)
        base.alpha_composite(garden)

        base.alpha_composite(radial_glow((size, size), (size * 0.50, size * 0.44), size * 0.50, size * 0.44,
                                         (148, 22, 58), alpha=0.40 + (0.0 if static_bg else 0.06 * magic_pulse), power=2.2))

        # Left & right floating broken silver rose-mirrors + thorny rose vines (held steady in static_bg mode for GIF compression).
        rose_glass = load_rgba("v4/rose-glass.webp")
        rose_vine = load_rgba("v4/rose-vine.webp")
        rose_sprig = load_rgba("v4/rose-sprig.webp")
        sway = 0.0 if static_bg else math.sin(phase) * 2.5
        paste_centered(base, rose_glass, (size * 0.12, size * 0.45), scale=0.88 * s, rotate=12 + sway * 0.6,
                       opacity=0.82, tint=(0.86, 0.74, 0.94))
        paste_centered(base, rose_glass, (size * 0.88, size * 0.44), scale=0.86 * s, rotate=-14 - sway * 0.6,
                       flip_x=True, opacity=0.82, tint=(0.86, 0.74, 0.94))
        paste_centered(base, rose_vine, (size * 0.14, size * 0.76), scale=0.98 * s, rotate=-18 + sway,
                       opacity=0.88, tint=(0.88, 0.72, 0.84))
        paste_centered(base, rose_vine, (size * 0.86, size * 0.75), scale=0.98 * s, rotate=18 - sway,
                       flip_x=True, opacity=0.88, tint=(0.88, 0.72, 0.84))
        paste_centered(base, rose_sprig, (size * 0.10, size * 0.24), scale=0.78 * s, rotate=-12 - sway * 0.7,
                       opacity=0.74, tint=(0.90, 0.68, 0.82))
        paste_centered(base, rose_sprig, (size * 0.91, size * 0.23), scale=0.76 * s, rotate=14 + sway * 0.7,
                       flip_x=True, opacity=0.74, tint=(0.90, 0.68, 0.82))

        # Giant upper-body Witch behind the mirror (feathered top hat edge, zero flat seam).
        witch = build_witch_portrait(smile=smile, eyes_glow=eyes_glow)
        witch_scale = 1.24 * s
        face_dx = (1046 - 1672 / 2.0) * witch_scale
        face_dy = (290 - 941 / 2.0) * witch_scale
        witch_center = (size * 0.505 - face_dx, size * 0.335 - face_dy)
        paste_centered(base, witch, witch_center, scale=witch_scale)

        # Atmospheric mist cushion behind the foreground execution mirror.
        base.alpha_composite(radial_glow((size, size), (size * 0.50, size * 0.65), size * 0.56, size * 0.24,
                                         (48, 22, 58), alpha=0.54, power=1.7))

        # Ornate silver execution mirror + imprisoned soul lifted cleanly above THE WITCH title.
        mirror = load_rgba("mirror.webp")
        soul = load_rgba("soul.webp")
        mirror_center = (size * 0.535, size * 0.645)
        mirror_scale = 0.76 * s
        base.alpha_composite(radial_glow((size, size), mirror_center, size * 0.16, size * 0.21,
                                         (188, 26, 68), alpha=0.46 + 0.18 * magic_pulse, power=1.8))
        paste_centered(base, mirror, mirror_center, scale=mirror_scale)
        paste_centered(base, soul, (mirror_center[0], mirror_center[1] + math.sin(phase) * 4.0 * s),
                       scale=(0.45 - 0.03 * magic_pulse) * s, opacity=0.92, tint=(0.94, 0.82, 1.0))
        draw_mirror_cracks(base, mirror_center, 138 * s, alpha=int(180 + 60 * magic_pulse))

        # Witch's reaching hand emerging from behind the mirror into the lower-left foreground.
        hand = feather_sleeve(load_rgba("v3/palm.webp"))
        hand_scale = (0.94 + 0.06 * hand_reach) * s
        hand_center = (size * (0.27 + 0.024 * hand_reach), size * (0.675 - 0.016 * hand_reach))
        paste_centered(base, hand, hand_center, scale=hand_scale, rotate=-8.0 + 4.5 * hand_reach)

        # Glowing crimson magic streams linking her palm & ruby ring into the mirror soul core.
        palm_tip = (hand_center[0] + 62 * s, hand_center[1] - 30 * s)
        draw_magic_filaments(base, palm_tip, mirror_center, count=5, spread=62 * s, width=3.4 * s, phase=phase)
        base.alpha_composite(radial_glow((size, size), palm_tip, 92 * s, 92 * s,
                                         (248, 72, 118), alpha=0.42 + 0.18 * magic_pulse, power=2.2))
        base.alpha_composite(radial_glow((size, size), mirror_center, 68 * s, 68 * s,
                                         (255, 136, 174), alpha=0.40 + 0.24 * magic_pulse, power=2.4))

        # Painted rose petals + atmospheric crimson embers (strictly protecting face/chin/title).
        petals = load_rgba("v4/petals.webp")
        paste_centered(base, petals, (size * 0.79, size * 0.60), scale=0.86 * s,
                       rotate=18 + (0.0 if static_bg else phase * 6), opacity=0.80)
        paste_centered(base, petals, (size * 0.16, size * 0.56), scale=0.64 * s,
                       rotate=-35 - (0.0 if static_bg else phase * 5), flip_x=True, opacity=0.70)
        draw_crimson_embers(base, mirror_center, seed=42, count=34, radius_min=195, radius_max=680, phase=phase, scale=s)

        apply_vignette(base, top=0.54, bottom=0.95, sides=0.34, bottom_start=0.60)

    if with_text:
        render_gradient_text(base, "SPIRELAB", size * 0.50, size * 0.058, round(46 * s), round(12 * s),
                             style="sub", top_color=(244, 224, 236), bot_color=(200, 166, 192),
                             glow_alpha=0.36, shadow_offset=(0, round(3 * s)))
        draw_gothic_rule(base, size * 0.50, size * 0.090, round(130 * s), diamond=round(6 * s), alpha=205)

        render_gradient_text(base, "THE WITCH", size * 0.50, size * 0.792, round(212 * s), round(10 * s),
                             style="display", top_color=IVORY_TOP, bot_color=IVORY_BOT,
                             glow_color=(212, 34, 74), glow_alpha=0.64, max_width=round(1380 * s),
                             shadow_offset=(0, round(8 * s)))
        draw_gothic_rule(base, size * 0.50, size * 0.860, round(410 * s), diamond=round(10 * s), alpha=235)
        render_gradient_text(base, "CRIMSON FINAL MIRROR", size * 0.50, size * 0.898, round(50 * s), round(7 * s),
                             style="sub", top_color=(244, 224, 236), bot_color=(204, 174, 198),
                             glow_color=(186, 32, 74), glow_alpha=0.42, max_width=round(1320 * s),
                             shadow_offset=(0, round(4 * s)))
        render_gradient_text(base, "血色终镜", size * 0.50, size * 0.949, round(58 * s), round(20 * s),
                             style="chinese", top_color=ROSE_TOP, bot_color=ROSE_BOT,
                             glow_color=(220, 26, 70), glow_alpha=0.68, shadow_offset=(0, round(4 * s)))

    return base.convert("RGB")


def compose_video_cover():
    """Compose a clean 1920x1080 16:9 trailer thumbnail directly from lossless V3/V4 art (zero HUD)."""
    w, h = 1920, 1080
    base = Image.new("RGBA", (w, h), (*DEEP_SHADOW, 255))
    garden = ImageOps.fit(load_rgba("v4/mirror-garden.webp"), (w, h), Image.Resampling.LANCZOS)
    garden = ImageEnhance.Brightness(ImageEnhance.Contrast(garden).enhance(1.08)).enhance(0.86)
    base.alpha_composite(garden)
    base.alpha_composite(radial_glow((w, h), (w * 0.52, h * 0.44), w * 0.46, h * 0.52,
                                     (152, 22, 58), alpha=0.42, power=2.1))

    rose_glass = load_rgba("v4/rose-glass.webp")
    rose_vine = load_rgba("v4/rose-vine.webp")
    paste_centered(base, rose_glass, (w * 0.13, h * 0.48), scale=0.92, rotate=10, opacity=0.82, tint=(0.86, 0.74, 0.94))
    paste_centered(base, rose_glass, (w * 0.87, h * 0.46), scale=0.90, rotate=-12, flip_x=True, opacity=0.82, tint=(0.86, 0.74, 0.94))
    paste_centered(base, rose_vine, (w * 0.12, h * 0.84), scale=0.95, rotate=-15, opacity=0.88, tint=(0.88, 0.70, 0.84))
    paste_centered(base, rose_vine, (w * 0.88, h * 0.82), scale=0.95, rotate=15, flip_x=True, opacity=0.88, tint=(0.88, 0.70, 0.84))

    witch = build_witch_portrait(smile=1.0, eyes_glow=0.96)
    witch_scale = 1.04
    face_dx = (1046 - 1672 / 2.0) * witch_scale
    face_dy = (290 - 941 / 2.0) * witch_scale
    paste_centered(base, witch, (w * 0.51 - face_dx, h * 0.34 - face_dy), scale=witch_scale)

    mirror = load_rgba("mirror.webp")
    soul = load_rgba("soul.webp")
    mirror_center = (w * 0.52, h * 0.72)
    base.alpha_composite(radial_glow((w, h), mirror_center, 220, 260, (190, 28, 72), alpha=0.46, power=1.9))
    paste_centered(base, mirror, mirror_center, scale=0.68)
    paste_centered(base, soul, mirror_center, scale=0.40, opacity=0.92)
    draw_mirror_cracks(base, mirror_center, 122, alpha=190)

    hand = feather_sleeve(load_rgba("v3/palm.webp"))
    hand_center = (w * 0.31, h * 0.71)
    paste_centered(base, hand, hand_center, scale=0.82, rotate=-8)
    draw_magic_filaments(base, (hand_center[0] + 55, hand_center[1] - 28), mirror_center, count=5, spread=58, width=3.2)

    petals = load_rgba("v4/petals.webp")
    paste_centered(base, petals, (w * 0.77, h * 0.62), scale=0.88, rotate=14, opacity=0.82)
    draw_crimson_embers(base, mirror_center, seed=77, count=36, radius_min=200, radius_max=720, scale=0.95)

    apply_vignette(base, top=0.38, bottom=0.94, sides=0.32, bottom_start=0.58)
    render_gradient_text(base, "SPIRELAB", w * 0.50, 68, 34, 11, style="sub",
                         top_color=(244, 224, 236), bot_color=(200, 166, 192), glow_alpha=0.35, shadow_offset=(0, 3))
    render_gradient_text(base, "THE WITCH", w * 0.50, 808, 162, 10, style="display",
                         top_color=IVORY_TOP, bot_color=IVORY_BOT, glow_alpha=0.65, max_width=1480, shadow_offset=(0, 7))
    draw_gothic_rule(base, w * 0.50, 892, 390, diamond=9, alpha=235)
    render_gradient_text(base, "CRIMSON FINAL MIRROR", w * 0.50, 942, 44, 7, style="sub",
                         top_color=(244, 224, 236), bot_color=(204, 174, 198), glow_alpha=0.45, shadow_offset=(0, 4))
    render_gradient_text(base, "血色终镜", w * 0.50, 1008, 48, 18, style="chinese",
                         top_color=ROSE_TOP, bot_color=ROSE_BOT, glow_alpha=0.68, shadow_offset=(0, 4))
    return base.convert("RGB")


def export_cover(out, external_art=None):
    master = compose_square_scene(1600, with_text=True, external_art=external_art)
    master_path = out / "cover-master-1600.png"
    master.save(master_path, optimize=True)

    # 800x800 Steam primary cover strictly under 1 MB (1,000,000 bytes).
    small = master.resize((800, 800), Image.Resampling.LANCZOS)
    upload = out / "image.png"
    small.save(upload, optimize=True, compress_level=9)
    rgb_bytes = upload.stat().st_size
    method = "rgb-24bit-lossless"
    if rgb_bytes >= 1_000_000:
        # Step 1: Posterize slightly to 6-bit-per-channel RGB? Actually 256-color FASTOCTREE + Floyd-Steinberg
        # gives sharper gradients at ~270 KB, or try 7-bit RGB first to keep truecolor if < 1 MB!
        arr7 = (np.asarray(small, dtype=np.uint8) & 0xFE)
        Image.fromarray(arr7).save(upload, optimize=True, compress_level=9)
        method = "rgb-7bit-truecolor"
        if upload.stat().st_size >= 1_000_000:
            small.quantize(colors=256, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.FLOYDSTEINBERG).save(
                upload, optimize=True, compress_level=9
            )
            method = "palette-256-dithered"
    if upload.stat().st_size >= 1_000_000:
        raise ValueError(f"800x800 preview exceeds Steam's 1 MB limit: {upload.stat().st_size} bytes")

    with Image.open(upload) as check:
        check.convert("RGB").resize((268, 268), Image.Resampling.LANCZOS).save(out / "thumbnail-268.png", optimize=True)
        check.convert("RGB").resize((160, 160), Image.Resampling.LANCZOS).save(out / "thumbnail-160.png", optimize=True)
    return {"cover_encoding": method, "cover_raw_24bit_bytes": rgb_bytes}


def run_cmd(out, *args):
    with (out / "ffmpeg-commands.log").open("a") as log:
        print(shlex.join(map(str, args)), file=log)
    return subprocess.run(args, check=True, text=True, stdout=subprocess.PIPE).stdout


def render_intro_clip(out, layers):
    """0.0-1.5s animated gothic intro: crimson eyes emerge from shadow, then SPIRELAB / THE WITCH appears cleanly below."""
    ffmpeg = resolve_tool("ffmpeg")
    w, h = 1920, 1080
    frames = int(FPS * 1.5)
    witch = build_witch_portrait(smile=0.92, eyes_glow=1.15)
    face_plate = Image.new("RGBA", (w, h), (9, 4, 14, 255))
    garden = ImageOps.fit(load_rgba("v4/mirror-garden.webp"), (w, h), Image.Resampling.LANCZOS)
    face_plate.alpha_composite(ImageEnhance.Brightness(garden).enhance(0.28))
    scale = 1.48
    face_dx = (1055 - 1672 / 2.0) * scale
    face_dy = (310 - 941 / 2.0) * scale
    paste_centered(face_plate, witch, (w * 0.50 - face_dx, h * 0.37 - face_dy), scale=scale)

    ys = np.linspace(0.0, 1.0, h, dtype=np.float32)[:, None]
    xs = np.linspace(0.0, 1.0, w, dtype=np.float32)[None, :]
    eye_l = np.exp(-(((xs - 0.435) / 0.11) ** 2 + ((ys - 0.415) / 0.09) ** 2))
    eye_r = np.exp(-(((xs - 0.555) / 0.11) ** 2 + ((ys - 0.315) / 0.09) ** 2))
    eyes_focus = np.clip(eye_l + eye_r, 0.0, 1.0)[..., None]
    face_oval = np.exp(-(((xs - 0.50) / 0.38) ** 2 + ((ys - 0.38) / 0.34) ** 2))[..., None]
    crimson_pulse = np.array([215.0, 26.0, 66.0], dtype=np.float32)

    title_overlay = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    apply_vignette(title_overlay, top=0.22, bottom=0.92, sides=0.25, bottom_start=0.58)
    render_gradient_text(title_overlay, "SPIRELAB", w * 0.50, 735, 34, 12, style="sub",
                         top_color=ROSE_TOP, bot_color=ROSE_BOT, glow_alpha=0.50, shadow_offset=(0, 3))
    render_gradient_text(title_overlay, "THE WITCH", w * 0.50, 868, 174, 12, style="display",
                         top_color=IVORY_TOP, bot_color=IVORY_BOT, glow_alpha=0.72, max_width=1500, shadow_offset=(0, 8))
    draw_gothic_rule(title_overlay, w * 0.50, 962, 340, diamond=8, alpha=220)
    title_arr = np.asarray(title_overlay, dtype=np.float32)
    title_rgb = title_arr[..., :3]
    title_a = title_arr[..., 3:4] / 255.0

    intro_clip = layers / "intro.mov"
    proc = subprocess.Popen(
        [
            ffmpeg, "-v", "error", "-nostdin", "-y",
            "-f", "rawvideo", "-vcodec", "rawvideo", "-pix_fmt", "rgb24",
            "-s", f"{w}x{h}", "-r", str(FPS), "-i", "-",
            "-c:v", "prores_ks", "-profile:v", "3", "-pix_fmt", "yuv422p10le", str(intro_clip),
        ],
        stdin=subprocess.PIPE,
    )
    assert proc.stdin is not None
    saved_preview = False
    for f in range(frames):
        t = f / FPS
        zoom = 1.0 + 0.075 * quintic(t / 1.5)
        eye_reveal = quintic(t / 0.58)
        face_reveal = quintic((t - 0.18) / 0.78)
        title_reveal = quintic((t - 0.42) / 0.56)
        fade_out = 1.0 - quintic((t - 1.35) / 0.15)

        cw = w / zoom
        ch = h / zoom
        x0 = (w - cw) * 0.5
        y0 = (h - ch) * 0.44
        cropped = face_plate.crop((x0, y0, x0 + cw, y0 + ch)).resize((w, h), Image.Resampling.BILINEAR)
        c_arr = np.asarray(cropped.convert("RGB"), dtype=np.float32)

        reveal_mask = np.clip(eyes_focus * eye_reveal * 1.15 + face_oval * face_reveal * 0.92 + face_reveal * 0.25, 0.0, 1.0)
        bg = np.array([10.0, 5.0, 15.0], dtype=np.float32)
        frame_rgb = bg * (1.0 - reveal_mask) + c_arr * reveal_mask
        frame_rgb += crimson_pulse * (eyes_focus * eye_reveal * (1.0 - 0.55 * face_reveal) * 0.32)

        ta = title_a * title_reveal
        frame_rgb = frame_rgb * (1.0 - ta) + title_rgb * ta
        frame_rgb *= fade_out
        frame_u8 = np.clip(frame_rgb, 0.0, 255.0).astype(np.uint8)
        if not saved_preview and f == int(FPS * 1.05):
            Image.fromarray(frame_u8).save(layers / "intro.png")
            saved_preview = True
        proc.stdin.write(frame_u8.tobytes())
    proc.stdin.close()
    if proc.wait() != 0:
        raise RuntimeError("Failed to encode animated intro clip")
    return intro_clip


def render_outro_clip(out, layers):
    """8.5-10.0s animated gothic outro: shimmering mirror shards dissipate over clean mirror hall + final title."""
    ffmpeg = resolve_tool("ffmpeg")
    w, h = 1920, 1080
    frames = int(FPS * 1.5)

    garden_base = Image.new("RGBA", (w, h), (*DEEP_SHADOW, 255))
    garden = ImageOps.fit(load_rgba("v4/mirror-garden.webp"), (w, h), Image.Resampling.LANCZOS)
    garden = ImageEnhance.Brightness(ImageEnhance.Contrast(garden).enhance(1.06)).enhance(0.42)
    garden_base.alpha_composite(garden)
    garden_base.alpha_composite(radial_glow((w, h), (w * 0.50, h * 0.48), 760, 420, (175, 28, 70), alpha=0.32, power=2.2))
    apply_vignette(garden_base, top=0.45, bottom=0.65, sides=0.45, bottom_start=0.50)

    title = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    render_gradient_text(title, "SPIRELAB", w * 0.50, 315, 36, 13, style="sub",
                         top_color=(244, 224, 236), bot_color=(200, 166, 192), glow_alpha=0.42, shadow_offset=(0, 3))
    render_gradient_text(title, "THE WITCH", w * 0.50, 465, 178, 12, style="display",
                         top_color=IVORY_TOP, bot_color=IVORY_BOT, glow_alpha=0.68, max_width=1520, shadow_offset=(0, 8))
    draw_gothic_rule(title, w * 0.50, 566, 440, diamond=10, alpha=240)
    render_gradient_text(title, "CRIMSON FINAL MIRROR", w * 0.50, 636, 50, 8, style="sub",
                         top_color=(244, 224, 236), bot_color=(204, 174, 198), glow_alpha=0.48, shadow_offset=(0, 4))
    render_gradient_text(title, "血色终镜", w * 0.50, 742, 56, 20, style="chinese",
                         top_color=ROSE_TOP, bot_color=ROSE_BOT, glow_alpha=0.72, shadow_offset=(0, 4))
    title_arr = np.asarray(title, dtype=np.float32)
    title_rgb = title_arr[..., :3]
    title_a = title_arr[..., 3:4] / 255.0

    rng = np.random.default_rng(108)
    shards = []
    for i in range(26):
        ang = rng.uniform(0, math.tau)
        r0 = rng.uniform(220, 520)
        speed = rng.uniform(90, 240)
        size = rng.uniform(10, 26)
        rot0 = rng.uniform(0, math.tau)
        spin = rng.uniform(-1.6, 1.6)
        crimson = (i % 3 == 0)
        shards.append((ang, r0, speed, size, rot0, spin, crimson))

    outro_clip = layers / "outro.mov"
    proc = subprocess.Popen(
        [
            ffmpeg, "-v", "error", "-nostdin", "-y",
            "-f", "rawvideo", "-vcodec", "rawvideo", "-pix_fmt", "rgb24",
            "-s", f"{w}x{h}", "-r", str(FPS), "-i", "-",
            "-c:v", "prores_ks", "-profile:v", "3", "-pix_fmt", "yuv422p10le", str(outro_clip),
        ],
        stdin=subprocess.PIPE,
    )
    assert proc.stdin is not None
    saved_preview = False
    for f in range(frames):
        t = f / FPS
        fade_in = quintic(t / 0.24)
        fade_out = 1.0 - quintic((t - 1.24) / 0.26)
        shard_alpha = (1.0 - quintic(t / 1.18)) * 0.78

        frame = garden_base.copy()
        if shard_alpha > 0.01:
            sl = Image.new("RGBA", (w, h), (0, 0, 0, 0))
            ds = ImageDraw.Draw(sl)
            for ang, r0, speed, size, rot0, spin, crimson in shards:
                dist = r0 + speed * t
                px = w * 0.50 + math.cos(ang) * dist
                py = h * 0.50 + math.sin(ang) * dist * 0.62 + 32 * t * t
                rot = rot0 + spin * t
                verts = [(size, 0), (size * 0.15, size * 0.42), (-size * 0.85, size * 0.15), (-size * 0.4, -size * 0.38)]
                poly = [(px + vx * math.cos(rot) - vy * math.sin(rot), py + vx * math.sin(rot) + vy * math.cos(rot))
                        for vx, vy in verts]
                a = int(195 * shard_alpha)
                fill = (240, 74, 118, a) if crimson else (218, 200, 235, a)
                ds.polygon(poly, fill=fill, outline=(255, 232, 240, a))
            frame.alpha_composite(sl)

        f_arr = np.asarray(frame.convert("RGB"), dtype=np.float32)
        ta = title_a * quintic(t / 0.34)
        f_arr = f_arr * (1.0 - ta) + title_rgb * ta
        f_arr *= fade_in * fade_out
        frame_u8 = np.clip(f_arr, 0.0, 255.0).astype(np.uint8)
        if not saved_preview and f == int(FPS * 0.65):
            Image.fromarray(frame_u8).save(layers / "outro.png")
            saved_preview = True
        proc.stdin.write(frame_u8.tobytes())
    proc.stdin.close()
    if proc.wait() != 0:
        raise RuntimeError("Failed to encode animated outro clip")
    return outro_clip


def atempo(speed):
    stages = []
    while speed < 0.5:
        stages.append("atempo=0.5")
        speed *= 2.0
    return ",".join([*stages, f"atempo={speed:.10f}"])


def export_trailer(gameplay, out, layers):
    ffmpeg = resolve_tool("ffmpeg")
    video_cover = compose_video_cover()
    video_cover.save(out / "video-cover.png", optimize=True)

    intro_clip = render_intro_clip(out, layers)
    outro_clip = render_outro_clip(out, layers)

    filters = [
        f"[1:v]fps={FPS},setpts=PTS-STARTPTS,format=yuv420p,setsar=1[intro]",
        f"[2:v]fps={FPS},setpts=PTS-STARTPTS,format=yuv420p,setsar=1[outro]",
    ]
    for i, (start, end, duration) in enumerate(SHOTS):
        speed = (end - start) / duration
        filters.append(
            f"[0:v]trim=start={start}:end={end},setpts=(PTS-STARTPTS)/{speed:.10f},"
            f"fps={FPS},tpad=stop_duration=0.15:stop_mode=clone,trim=duration={duration},"
            f"scale=1920:1080:force_original_aspect_ratio=increase:flags=lanczos,"
            f"crop=1920:1080,setsar=1,format=yuv420p[v{i}]"
        )
        filters.append(
            f"[0:a]atrim=start={start}:end={end},asetpts=PTS-STARTPTS,{atempo(speed)},"
            f"apad=whole_dur={duration},atrim=duration={duration},"
            f"afade=t=in:d=0.018,afade=t=out:st={duration - 0.024}:d=0.024,"
            f"volume=6dB,aformat=sample_rates=48000:channel_layouts=stereo[a{i}]"
        )
    filters.extend([
        "[intro][v0][v1][v2][v3][outro]concat=n=6:v=1:a=0[video]",
        "[a0][a1][a2][a3]concat=n=4:v=0:a=1,adelay=1500:all=1,apad=whole_dur=10[game]",
        "[3:a]asplit=2[air0][tail0]",
        "[air0]atrim=duration=1.80,aformat=sample_rates=48000:channel_layouts=stereo,"
        "volume=-3dB,afade=t=in:d=0.18,afade=t=out:st=1.32:d=0.48,apad=whole_dur=10[air]",
        "[tail0]atrim=start=2.72:end=3.55,asetpts=PTS-STARTPTS,"
        "aformat=sample_rates=48000:channel_layouts=stereo,volume=-5dB,adelay=8600:all=1,apad=whole_dur=10[tail]",
        "[4:a]atrim=duration=1.30,afade=t=out:st=0.95:d=0.35,volume=-7dB,"
        "aformat=sample_rates=48000:channel_layouts=stereo,adelay=7020:all=1,apad=whole_dur=10[impact]",
        "[game][air][tail][impact]amix=inputs=4:normalize=0,alimiter=limit=0.84:latency=1,atrim=duration=10[audio]",
    ])
    master = out / "trailer-master.mov"
    run_cmd(
        out, ffmpeg, "-v", "error", "-nostdin", "-y",
        "-i", gameplay, "-i", intro_clip, "-i", outro_clip,
        "-i", AUDIO_DIR / "v4/ritual.wav", "-i", AUDIO_DIR / "mirror-shatter.wav",
        "-filter_complex_threads", "3", "-filter_complex", ";".join(filters),
        "-map", "[video]", "-map", "[audio]", "-t", "10",
        "-c:v", "prores_ks", "-profile:v", "3", "-pix_fmt", "yuv422p10le", "-c:a", "pcm_s24le", master,
    )
    run_cmd(
        out, ffmpeg, "-v", "error", "-nostdin", "-y", "-i", master,
        "-c:v", "libx264", "-preset", "slow", "-crf", "17", "-pix_fmt", "yuv420p",
        "-c:a", "aac", "-b:a", "256k", "-movflags", "+faststart", out / "trailer.mp4",
    )


def export_gif(out, layers):
    """Build a 2.4s 1:1 square seamless loop GIF strictly under 1 MB using static backdrop delta efficiency."""
    ffmpeg = resolve_tool("ffmpeg")
    gif_frames_dir = layers / "gif_frames"
    gif_frames_dir.mkdir(parents=True, exist_ok=True)
    duration = 2.4
    attempts = [
        (540, 12, 192),
        (480, 12, 160),
        (480, 10, 128),
        (420, 10, 128),
        (384, 10, 112),
        (320, 9, 96),
    ]
    chosen = None
    gif_path = out / "cover.gif"
    for dim, gif_fps, colors in attempts:
        for old in gif_frames_dir.glob("*.png"):
            old.unlink()
        total = int(round(duration * gif_fps))
        for idx in range(total):
            u = idx / total
            phase = u * math.tau
            wave = 0.5 - 0.5 * math.cos(phase)
            frame = compose_square_scene(
                dim,
                smile=0.10 + 0.90 * wave,
                eyes_glow=0.22 + 1.08 * wave,
                hand_reach=wave,
                magic_pulse=wave,
                phase=phase,
                with_text=True,
                static_bg=True,
            )
            frame.save(gif_frames_dir / f"frame_{idx:03d}.png")
        palette = layers / "gif-palette.png"
        run_cmd(
            out, ffmpeg, "-v", "error", "-nostdin", "-y",
            "-framerate", str(gif_fps), "-i", str(gif_frames_dir / "frame_%03d.png"),
            "-vf", f"palettegen=max_colors={colors}:stats_mode=diff", str(palette),
        )
        run_cmd(
            out, ffmpeg, "-v", "error", "-nostdin", "-y",
            "-framerate", str(gif_fps), "-i", str(gif_frames_dir / "frame_%03d.png"),
            "-i", str(palette),
            "-lavfi", f"fps={gif_fps}[x];[x][1:v]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle",
            "-loop", "0", str(gif_path),
        )
        size_bytes = gif_path.stat().st_size
        if size_bytes < 1_000_000:
            chosen = {
                "width": dim,
                "height": dim,
                "fps": gif_fps,
                "duration_sec": duration,
                "frames": total,
                "colors": colors,
                "bytes": size_bytes,
            }
            break
    if chosen is None:
        raise ValueError("Unable to fit 1:1 animated GIF under Steam's 1 MB limit")
    return chosen


def export_review_sheets(out, layers):
    """Render Steam Workshop UI adaptation review sheet & trailer storyboard contact sheet."""
    sheet = Image.new("RGB", (1800, 1080), (27, 40, 56))
    d = ImageDraw.Draw(sheet)
    header_font = font(26, style="sub")
    label_font = font(18, style="sub")
    small_font = font(15, style="sub")

    d.rectangle((0, 0, 1800, 58), fill=(16, 24, 34))
    d.text((32, 16), "Steam Workshop Adaptation Review — SpireLab: The Witch (Crimson Final Mirror V4)",
           font=header_font, fill=(218, 228, 238))

    cover = Image.open(out / "image.png").convert("RGB")
    vcover = Image.open(out / "video-cover.png").convert("RGB")

    # Panel A: Workshop Browse Grid Card (268x268 square tile).
    d.rounded_rectangle((36, 86, 360, 462), radius=4, fill=(16, 24, 34), outline=(42, 64, 88))
    d.text((52, 98), "1. Workshop Grid Tile (268x268)", font=label_font, fill=(143, 152, 160))
    sheet.paste(cover.resize((268, 268), Image.Resampling.LANCZOS), (64, 130))
    d.text((64, 406), "SpireLab — 魔女 / The Witch", font=font(16, style="chinese"), fill=(226, 234, 242))
    d.text((64, 434), "Slay the Spire 2  ·  Workshop", font=small_font, fill=(102, 192, 244))

    # Panel B: Compact List / Sidebar Thumbnail (160x160).
    d.rounded_rectangle((36, 478, 360, 680), radius=4, fill=(16, 24, 34), outline=(42, 64, 88))
    d.text((52, 488), "2. Compact Thumbnail (160x160)", font=label_font, fill=(143, 152, 160))
    sheet.paste(cover.resize((160, 160), Image.Resampling.LANCZOS), (118, 514))

    # Panel C: Detail Page Primary Preview (1:1 512x512).
    d.rounded_rectangle((388, 86, 956, 680), radius=4, fill=(16, 24, 34), outline=(42, 64, 88))
    d.text((406, 98), "3. Item Detail Primary Preview (800x800 @ 512x512)", font=label_font, fill=(143, 152, 160))
    sheet.paste(cover.resize((512, 512), Image.Resampling.LANCZOS), (416, 138))

    # Panel D: Detail Page Video Showcase (16:9 + gallery strip).
    d.rounded_rectangle((984, 86, 1764, 680), radius=4, fill=(16, 24, 34), outline=(42, 64, 88))
    d.text((1002, 98), "4. Video Showcase (16:9 YouTube Preview + Strip)", font=label_font, fill=(143, 152, 160))
    sheet.paste(vcover.resize((720, 405), Image.Resampling.LANCZOS), (1014, 138))
    cx, cy = 1014 + 360, 138 + 202
    d.rounded_rectangle((cx - 38, cy - 26, cx + 38, cy + 26), radius=10, fill=(22, 22, 26), outline=(220, 220, 220))
    d.polygon([(cx - 10, cy - 13), (cx + 16, cy), (cx - 10, cy + 13)], fill=(255, 255, 255))

    strip_y = 566
    strip_items = [cover, vcover]
    if (layers / "intro.png").exists():
        strip_items.append(Image.open(layers / "intro.png").convert("RGB"))
    if (layers / "outro.png").exists():
        strip_items.append(Image.open(layers / "outro.png").convert("RGB"))
    for i, im in enumerate(strip_items):
        sx = 1014 + i * 182
        thumb = ImageOps.fit(im, (166, 93), Image.Resampling.LANCZOS)
        sheet.paste(thumb, (sx, strip_y))
        d.rectangle((sx, strip_y, sx + 166, strip_y + 93), outline=(102, 192, 244) if i == 0 else (64, 82, 102), width=2)

    # Panel E: Optional GIF sampled frames strip.
    gif_path = out / "cover.gif"
    d.rounded_rectangle((36, 704, 1764, 1052), radius=4, fill=(16, 24, 34), outline=(42, 64, 88))
    if gif_path.exists():
        gif_im = Image.open(gif_path)
        n_frames = getattr(gif_im, "n_frames", 1)
        d.text(
            (52, 716),
            f"5. Optional 1:1 Animated GIF ({gif_im.width}x{gif_im.height}, {n_frames} frames, {gif_path.stat().st_size:,} bytes)",
            font=label_font,
            fill=(143, 152, 160),
        )
        for i in range(6):
            idx = min(n_frames - 1, round(i * (n_frames - 1) / 5))
            gif_im.seek(idx)
            frame_rgb = gif_im.convert("RGB").resize((266, 266), Image.Resampling.LANCZOS)
            fx = 54 + i * 284
            sheet.paste(frame_rgb, (fx, 752))
            d.text((fx + 6, 1022), f"frame {idx + 1}/{n_frames}", font=small_font, fill=(160, 176, 192))
    else:
        d.text((52, 716), "5. Animated GIF skipped", font=label_font, fill=(143, 152, 160))

    sheet.save(out / "workshop-adaptation-review.png", optimize=True)

    # 2. Trailer 6-beat contact sheet from the final encoded trailer.mp4.
    trailer_mp4 = out / "trailer.mp4"
    if trailer_mp4.exists():
        ffmpeg = resolve_tool("ffmpeg")
        beats = [
            (0.85, "0.85s Intro — Eyes & Title"),
            (2.15, "2.15s Gameplay Lock & Pullback"),
            (4.25, "4.25s Mirror Witch Wicked Smile"),
            (6.10, "6.10s Reach & Portrait Dissolve"),
            (7.12, "7.12s Fist Clench & Shatter Climax"),
            (9.20, "9.20s Final Title Outro"),
        ]
        board = Image.new("RGB", (640 * 3, 360 * 2 + 64), (16, 12, 22))
        db = ImageDraw.Draw(board)
        for i, (sec, caption) in enumerate(beats):
            tmp_frame = layers / f"beat_{i}.png"
            run_cmd(out, ffmpeg, "-v", "error", "-nostdin", "-y", "-ss", str(sec), "-i", trailer_mp4, "-frames:v", "1", tmp_frame)
            tile = Image.open(tmp_frame).convert("RGB").resize((640, 360), Image.Resampling.LANCZOS)
            gx = (i % 3) * 640
            gy = (i // 3) * 392
            board.paste(tile, (gx, gy))
            db.rectangle((gx, gy + 360, gx + 640, gy + 392), fill=(18, 12, 26))
            db.text((gx + 16, gy + 366), caption, font=label_font, fill=(242, 226, 236))
        board.save(out / "trailer-storyboard-review.png", optimize=True)


def inspect_deliverables(out, extras):
    ffprobe = resolve_tool("ffprobe")
    manifest = {"meta": extras, "assets": {}}
    for asset in sorted(out.glob("*")):
        if not asset.is_file() or asset.name in {"ffmpeg-commands.log", "media-check.json"}:
            continue
        sha256 = hashlib.sha256(asset.read_bytes()).hexdigest()
        size_bytes = asset.stat().st_size
        if asset.suffix in {".png", ".gif"}:
            with Image.open(asset) as pic:
                info = {
                    "bytes": size_bytes,
                    "under_1mb": size_bytes < 1_000_000,
                    "width": pic.width,
                    "height": pic.height,
                    "format": pic.format,
                    "mode": pic.mode,
                    "sha256": sha256,
                }
                if asset.suffix == ".gif":
                    info["frames"] = getattr(pic, "n_frames", 1)
                    info["is_animated"] = getattr(pic, "is_animated", False)
                manifest["assets"][asset.name] = info
        elif asset.suffix in {".mov", ".mp4"}:
            probe = json.loads(run_cmd(
                out, ffprobe, "-v", "error",
                "-show_entries", "format=duration:stream=codec_name,codec_type,width,height,r_frame_rate,sample_rate,channels",
                "-of", "json", asset,
            ))
            manifest["assets"][asset.name] = {"bytes": size_bytes, "sha256": sha256, **probe}
    (out / "media-check.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n")
    return manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--art", type=Path, help="Optional external square master plate; defaults to lossless V3/V4 composite")
    parser.add_argument("--gameplay", type=Path, default=DEFAULT_GAMEPLAY, help="Recorded V4 finisher video")
    parser.add_argument("--out", type=Path, required=True, help="Output directory")
    parser.add_argument("--overwrite", action="store_true", help="Allow overwriting an existing output directory")
    parser.add_argument("--skip-cover", action="store_true")
    parser.add_argument("--skip-trailer", action="store_true")
    parser.add_argument("--skip-gif", action="store_true")
    parser.add_argument("--skip-review", action="store_true")
    args = parser.parse_args()

    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=True)
    if any(out.iterdir()) and not args.overwrite:
        parser.error("--out must be empty unless --overwrite is specified")

    layers = out / "layers"
    layers.mkdir(exist_ok=True)
    extras = {}
    if not args.skip_cover:
        extras.update(export_cover(out, external_art=args.art.resolve() if args.art else None))
    if not args.skip_trailer:
        gameplay = args.gameplay.resolve()
        if not gameplay.is_file():
            parser.error(f"Recorded V4 gameplay video not found: {gameplay}")
        export_trailer(gameplay, out, layers)
    if not args.skip_gif:
        extras["gif"] = export_gif(out, layers)
    if not args.skip_review and (out / "image.png").exists() and (out / "video-cover.png").exists():
        export_review_sheets(out, layers)

    manifest = inspect_deliverables(out, extras)
    print(json.dumps(manifest, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()

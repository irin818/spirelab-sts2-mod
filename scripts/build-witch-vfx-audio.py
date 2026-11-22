#!/usr/bin/env python3
"""Reproducibly synthesize material-matched gothic SFX for The Witch V2.1 (Crimson Mirror Nocturne).

Outputs 44.1kHz 16-bit mono WAV files under SpireLab/SpireLab/audio/witch/vfx/.
Designed to be crisp, cohesive, subtle, and non-fatiguing under rapid play.
"""

from __future__ import annotations

import math
import wave
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parent.parent
OUT_DIR = ROOT / "SpireLab/SpireLab/audio/witch/vfx"
SR = 44100


def write_wav(name: str, samples: np.ndarray) -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    peak = float(np.max(np.abs(samples))) if len(samples) else 1.0
    if peak > 0.92:
        samples = samples * (0.92 / peak)
    pcm = np.clip(samples * 32767.0, -32767, 32767).astype(np.int16)
    path = OUT_DIR / name
    with wave.open(str(path), "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(SR)
        wf.writeframes(pcm.tobytes())
    print(f"Wrote {path.relative_to(ROOT)} ({len(samples)/SR:.2f}s, {path.stat().st_size} bytes)")


def bandpass_noise(rng: np.random.Generator, n: int, lo_hz: float, hi_hz: float) -> np.ndarray:
    noise = rng.standard_normal(n)
    freqs = np.fft.rfftfreq(n, 1.0 / SR)
    spec = np.fft.rfft(noise)
    mask = (freqs >= lo_hz) & (freqs <= hi_hz)
    # Soft spectral roll-off
    taper = np.where(mask, 1.0, 0.0)
    out = np.fft.irfft(spec * taper, n=n)
    mx = np.max(np.abs(out)) + 1e-9
    return out / mx


def build_potion_throw(rng: np.random.Generator) -> np.ndarray:
    n = int(SR * 0.34)
    t = np.linspace(0, n / SR, n, endpoint=False)
    # Soft liquid whoosh (0.00..0.16s) + delicate crystalline glass clink/splash (0.14..0.34s)
    whoosh = bandpass_noise(rng, n, 450, 2200) * np.exp(-((t - 0.07) / 0.045) ** 2) * 0.38
    glass_t = np.clip(t - 0.14, 0, None)
    glass_env = np.where(t >= 0.14, np.exp(-glass_t * 24.0), 0.0)
    glass_tones = (
        np.sin(2 * np.pi * 2480 * glass_t) * 0.32
        + np.sin(2 * np.pi * 3710 * glass_t) * 0.24
        + np.sin(2 * np.pi * 5180 * glass_t) * 0.16
    ) * glass_env
    splash = bandpass_noise(rng, n, 1200, 6500) * np.where(t >= 0.145, np.exp(-(t - 0.145) * 18.0), 0.0) * 0.34
    return (whoosh + glass_tones + splash) * 0.85


def build_potion_burst(rng: np.random.Generator) -> np.ndarray:
    n = int(SR * 0.28)
    t = np.linspace(0, n / SR, n, endpoint=False)
    # Compressed crimson alchemy thump + crystalline sparkle decay
    freq = 210 * np.exp(-t * 14.0) + 58.0
    phase = 2 * np.pi * np.cumsum(freq) / SR
    thump = np.sin(phase) * np.exp(-t * 18.0) * 0.55
    crackle = bandpass_noise(rng, n, 1800, 7200) * np.exp(-t * 22.0) * 0.35
    chime = (np.sin(2 * np.pi * 1568 * t) * 0.18 + np.sin(2 * np.pi * 2349 * t) * 0.12) * np.exp(-t * 15.0)
    return (thump + crackle + chime) * 0.85


def build_mirror_slash(rng: np.random.Generator) -> np.ndarray:
    n = int(SR * 0.26)
    t = np.linspace(0, n / SR, n, endpoint=False)
    # Crisp cold-silver blade sweep + high glassy harmonic shimmer
    sweep = bandpass_noise(rng, n, 2400, 9200) * np.exp(-((t - 0.04) / 0.035) ** 2) * 0.45
    ring_env = np.exp(-t * 16.0) * (1.0 - np.exp(-t * 180.0))
    ring = (
        np.sin(2 * np.pi * 1975 * t) * 0.28
        + np.sin(2 * np.pi * 2960 * t) * 0.24
        + np.sin(2 * np.pi * 4435 * t) * 0.15
    ) * ring_env
    return (sweep + ring) * 0.80


def build_grimoire_page(rng: np.random.Generator) -> np.ndarray:
    n = int(SR * 0.24)
    t = np.linspace(0, n / SR, n, endpoint=False)
    # Crisp parchment flutter + gentle silver-ink rune tone
    flutter = bandpass_noise(rng, n, 900, 4800) * (
        np.exp(-((t - 0.035) / 0.025) ** 2) * 0.42 + np.exp(-((t - 0.095) / 0.032) ** 2) * 0.28
    )
    rune = (np.sin(2 * np.pi * 880 * t) * 0.18 + np.sin(2 * np.pi * 1318.5 * t) * 0.14) * np.exp(-t * 14.0)
    return (flutter + rune) * 0.82


def build_counterpoint_chime(rng: np.random.Generator) -> np.ndarray:
    n = int(SR * 0.30)
    t = np.linspace(0, n / SR, n, endpoint=False)
    # Distinct twin-note gothic bell interval: E5 (659.25 Hz) -> B5 (987.77 Hz) at +0.065s
    n1 = (np.sin(2 * np.pi * 659.25 * t) * 0.34 + np.sin(2 * np.pi * 1318.5 * t) * 0.14) * np.exp(-t * 16.0)
    t2 = np.clip(t - 0.065, 0, None)
    e2 = np.where(t >= 0.065, np.exp(-t2 * 13.0) * (1.0 - np.exp(-t2 * 260.0)), 0.0)
    n2 = (np.sin(2 * np.pi * 987.77 * t2) * 0.36 + np.sin(2 * np.pi * 1975.5 * t2) * 0.16) * e2
    shimmer = bandpass_noise(rng, n, 3500, 8000) * e2 * 0.08
    return (n1 + n2 + shimmer) * 0.82


def build_exhaust_ash(rng: np.random.Generator) -> np.ndarray:
    n = int(SR * 0.26)
    t = np.linspace(0, n / SR, n, endpoint=False)
    # Delicate crimson-thread dissolve & dark ash whisper (distinct from page discard)
    whisper = bandpass_noise(rng, n, 1400, 6200) * np.exp(-((t - 0.08) / 0.07) ** 2) * 0.42
    low = np.sin(2 * np.pi * (310 - 110 * t / 0.26) * t) * np.exp(-t * 14.0) * 0.24
    return (whisper + low) * 0.78


def build_gamble_siphon(rng: np.random.Generator) -> np.ndarray:
    n = int(SR * 0.19)
    t = np.linspace(0, n / SR, n, endpoint=False)
    # Tight crimson pulse (0..0.07s) -> crisp energy spark (0.07..0.19s), ultra-snappy for repeat plays
    pulse = np.sin(2 * np.pi * (145 + 120 * t) * t) * np.exp(-((t - 0.03) / 0.025) ** 2) * 0.44
    t2 = np.clip(t - 0.065, 0, None)
    e2 = np.where(t >= 0.065, np.exp(-t2 * 24.0), 0.0)
    spark = (np.sin(2 * np.pi * 1174.66 * t2) * 0.30 + np.sin(2 * np.pi * 1760.0 * t2) * 0.20) * e2
    return (pulse + spark) * 0.80


def main() -> None:
    rng = np.random.default_rng(20261123)
    write_wav("potion_throw.wav", build_potion_throw(rng))
    write_wav("potion_burst.wav", build_potion_burst(rng))
    write_wav("mirror_slash.wav", build_mirror_slash(rng))
    write_wav("grimoire_page.wav", build_grimoire_page(rng))
    write_wav("counterpoint_chime.wav", build_counterpoint_chime(rng))
    write_wav("exhaust_ash.wav", build_exhaust_ash(rng))
    write_wav("gamble_siphon.wav", build_gamble_siphon(rng))


if __name__ == "__main__":
    main()

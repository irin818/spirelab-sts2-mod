"""Generate the Witch's original short combat cues as mono PCM assets."""

import math
import random
import struct
import wave
from pathlib import Path

OUT = Path(__file__).resolve().parents[3] / "SpireLab/SpireLab/audio/witch"
RATE = 22050

CUES = {
    "enlighten": (0.32, (524, 784, 1178), 0.02),
    "mirror": (0.44, (213, 497, 1231), 0.22),
    "reflect": (0.20, (351, 773, 1793), 0.25),
    "desire": (0.34, (111, 165, 277), 0.09),
    "energy": (0.22, (330, 660, 990), 0.04),
}

OUT.mkdir(parents=True, exist_ok=True)
for name, (duration, partials, grit) in CUES.items():
    rng = random.Random(name)
    frames = bytearray()
    for index in range(int(duration * RATE)):
        time = index / RATE
        envelope = min(1.0, time / 0.012) * (1 - time / duration) ** 2
        tone = sum(math.sin(math.tau * hz * time) / (i + 2) for i, hz in enumerate(partials))
        audio = envelope * (tone + grit * rng.uniform(-1, 1))
        frames.extend(struct.pack("<h", int(6700 * audio)))
    with wave.open(str(OUT / f"{name}.wav"), "wb") as cue:
        cue.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
        cue.writeframes(frames)

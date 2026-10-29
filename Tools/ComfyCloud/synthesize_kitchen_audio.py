#!/usr/bin/env python3
"""Deterministic placeholder foley, authored locally; these are not recorded or Cloud-generated sounds."""
import math
import random
import struct
import wave
from pathlib import Path


def write(path, seconds, kind):
    rng = random.Random({"ceramic": 409, "egg": 410, "sizzle": 411}[kind])
    rate = 24000
    samples = []
    previous = 0.0
    for i in range(int(rate * seconds)):
        t = i / rate
        noise = rng.uniform(-1, 1)
        if kind == "sizzle":
            # Fade both ends so the one-second loop has no boundary click.
            edge = min(1, t * 50, (seconds - t) * 50)
            value = (noise - previous) * .12 * edge
        else:
            impulse = math.exp(-t * (25 if kind == "egg" else 12))
            value = noise * impulse * .35
            if kind == "ceramic":
                value += sum(math.sin(2 * math.pi * f * t) * math.exp(-t * d) * .08
                             for f, d in ((1800, 8), (2900, 13), (4100, 20)))
        previous = noise
        samples.append(struct.pack("<h", int(max(-1, min(1, value)) * 28000)))
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1); output.setsampwidth(2); output.setframerate(rate)
        output.writeframes(b"".join(samples))


if __name__ == "__main__":
    folder = Path(__file__).resolve().parents[2] / "Assets/_Project/Art/Cooking"
    for kind, duration in (("ceramic", .5), ("egg", .2), ("sizzle", 1)):
        write(folder / ("SFX_" + kind + ".wav"), duration, kind)

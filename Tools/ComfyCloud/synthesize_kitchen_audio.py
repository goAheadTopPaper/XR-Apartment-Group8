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


def write_interaction(path, duration, seed, body, click, scrape):
    """Layer damped resonances, a dry transient and shaped broadband friction."""
    rate = 24000
    rng = random.Random(seed)
    low, previous = 0.0, 0.0
    samples = []
    for i in range(int(rate * duration)):
        t = i / rate
        noise = rng.uniform(-1, 1)
        low += .12 * (noise - low)
        bright = noise - previous
        previous = noise
        value = sum(level * math.sin(math.tau * hz * t) * math.exp(-damping * t)
                    for hz, level, damping in body)
        value += click * bright * math.exp(-95 * t)
        envelope = min(1, t * 100) * math.exp(-t * 28)
        value += scrape * (low + .23 * bright) * envelope
        fade = min(1, t * 850, (duration - t) * 190)
        samples.append(struct.pack("<h", round(max(-1, min(1, value * fade)) * 30000)))
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(rate)
        output.writeframes(b"".join(samples))


if __name__ == "__main__":
    folder = Path(__file__).resolve().parents[2] / "Assets/_Project/Art/Cooking"
    for kind, duration in (("ceramic", .5), ("egg", .2), ("sizzle", 1)):
        write(folder / ("SFX_" + kind + ".wav"), duration, kind)
    interaction = (
        ("Grip", .10, ((154, .15, 48), (770, .08, 71)), .13, .24),
        ("Impact", .24, ((125, .28, 27), (620, .12, 54), (1745, .07, 66)), .21, .12),
        ("SoftImpact", .14, ((103, .32, 54), (246, .09, 61)), .08, .19),
        ("Unlatch", .17, ((164, .17, 30), (560, .13, 59), (1120, .11, 65)), .21, .25),
        ("Close", .24, ((86, .39, 23), (235, .14, 41), (710, .09, 54)), .18, .11),
        ("Cut", .15, ((176, .16, 43), (965, .07, 58)), .16, .39),
        ("Board", .16, ((221, .26, 44), (477, .15, 51), (906, .08, 72)), .24, .07),
        ("Stove", .23, ((278, .17, 50), (1310, .13, 68)), .22, .36),
        ("Menu", .10, ((690, .17, 62), (1040, .1, 81)), .04, .03),
        ("Step", .22, ((81, .25, 32), (142, .15, 47), (310, .07, 65)), .12, .28),
    )
    for index, (name, duration, body, click, scrape) in enumerate(interaction):
        write_interaction(folder / ("SFX_" + name + ".wav"), duration, 821 + index, body, click, scrape)

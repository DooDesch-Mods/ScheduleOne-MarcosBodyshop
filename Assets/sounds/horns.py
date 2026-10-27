"""Synthesises the four plain horns of the Horn category as 16-bit mono PCM at 22050 Hz.

    python horns.py [wav-dir]

Writes twotone.pcm (a car's two-tone horn), airhorn.pcm (a truck's air horn), beep.pcm (a short beep) and
eurofanfare.pcm (a four-note air-horn tune) next to this script, the format HornShop.Pcm reads; with a folder argument
also the same four as .wav for listening. Sines, shaped noise and a fixed seed, so a run gives the same files again.
Needs numpy.
"""
import os
import sys
import wave

import numpy as np

RATE = 22050
PEAK = 10 ** (-1.5 / 20)  # -1.5 dBFS, under the -1 dBFS ceiling
rng = np.random.default_rng(1717)


def seconds(n):
    return int(round(n * RATE))


def norm(x, peak=PEAK):
    return x * (peak / np.max(np.abs(x)))


def band(x, lo, hi, soft=0.25):
    """Band-pass by masking the spectrum, with soft edges."""
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / RATE)
    mask = np.ones_like(f)
    if lo > 0:
        mask *= 1 / (1 + (lo / np.maximum(f, 1e-3)) ** (2 / soft))
    if hi < RATE / 2:
        mask *= 1 / (1 + (f / hi) ** (2 / soft))
    return np.fft.irfft(spec * mask, len(x))


def envelope(n, attack, release):
    t = np.arange(n) / RATE
    length = n / RATE
    return np.minimum(t / attack, 1) * np.clip((length - t) / release, 0, 1)


def tone(freq, n, harmonics, tilt, scoop=0.0, scoop_time=0.06, vibrato=0.0):
    """
    A buzzing horn tone: harmonics over 1/n^tilt, with an optional scoop (the pitch starts this fraction low and
    rises within scoop_time, as a horn does while the air pressure builds) and a slight vibrato.
    """
    t = np.arange(n) / RATE
    bend = 1 - scoop * np.exp(-t / scoop_time)
    wobble = 1 + vibrato * np.sin(2 * np.pi * 5.5 * t)
    phase = 2 * np.pi * np.cumsum(freq * bend * wobble) / RATE
    out = np.zeros(n)
    for k in range(1, harmonics + 1):
        if freq * k > RATE / 2 * 0.9:
            break
        out += np.sin(k * phase) / k ** tilt
    return out


def twotone():
    """A car's twin disc horn: two notes a major third apart, buzzy and a little gritty, held for 0.7 s."""
    n = seconds(0.7)
    low = tone(349, n, 24, 0.75, vibrato=0.002)
    high = tone(440, n, 24, 0.75, vibrato=0.002)
    body = np.tanh(1.8 * norm(low + high, 1.0))
    # The disc's resonance: a broad lift around 2 kHz over the plain buzz.
    body = 0.6 * norm(body) + 0.4 * norm(band(body, 1400, 3200))
    return norm(body * envelope(n, 0.012, 0.05))


def airhorn():
    """A truck's air horn: three trumpets in a low chord, scooping up as the air comes, with a breath of air under it."""
    n = seconds(1.2)
    chord = sum(tone(f, n, 30, 0.9, scoop=0.07, scoop_time=0.07, vibrato=0.003) for f in (185, 233, 277))
    chord = np.tanh(1.4 * norm(chord, 1.0))
    t = np.arange(n) / RATE
    air = band(rng.standard_normal(n), 900, 4000) * np.exp(-t / 0.25)
    out = norm(chord) + 0.08 * norm(air)
    return norm(out * envelope(n, 0.04, 0.12))


def beep():
    """A short beep of a small car, 0.16 s."""
    n = seconds(0.16)
    body = tone(620, n, 9, 1.2)
    # Held down to the level of the other horns: a near-square wave is loud for its peak.
    return norm(np.tanh(1.3 * norm(body, 1.0)) * envelope(n, 0.006, 0.03), PEAK * 0.55)


def eurofanfare():
    """Four air-horn notes going up, G C E G, the last one held."""
    notes = ((392, 0.15), (523, 0.15), (659, 0.15), (784, 0.5))
    parts = []
    for f, length in notes:
        n = seconds(length)
        body = tone(f, n, 20, 0.85, scoop=0.03, scoop_time=0.02, vibrato=0.002)
        body = np.tanh(1.6 * norm(body, 1.0))
        parts.append(norm(body) * envelope(n, 0.008, 0.02))
    return norm(np.concatenate(parts), PEAK * 0.7)


def pcm(x):
    return np.round(np.clip(x, -1, 1) * 32767).astype("<i2").tobytes()


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    wav_dir = sys.argv[1] if len(sys.argv) > 1 else None
    if wav_dir:
        os.makedirs(wav_dir, exist_ok=True)
    for name, make in (("twotone", twotone), ("airhorn", airhorn), ("beep", beep), ("eurofanfare", eurofanfare)):
        data = pcm(make())
        with open(os.path.join(here, name + ".pcm"), "wb") as f:
            f.write(data)
        samples = np.frombuffer(data, "<i2").astype(np.float64) / 32768
        peak_db = 20 * np.log10(np.max(np.abs(samples)))
        rms_db = 20 * np.log10(np.sqrt(np.mean(samples ** 2)))
        print("%s: %d samples, %.3f s, peak %.2f dBFS, rms %.1f dBFS" % (name, len(samples), len(samples) / RATE, peak_db, rms_db))
        if wav_dir:
            with wave.open(os.path.join(wav_dir, name + ".wav"), "wb") as w:
                w.setnchannels(1)
                w.setsampwidth(2)
                w.setframerate(RATE)
                w.writeframes(data)


if __name__ == "__main__":
    main()

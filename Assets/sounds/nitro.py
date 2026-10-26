"""Synthesises the three nitro sounds of the Nitrous Kit as 16-bit mono PCM at 22050 Hz.

    python nitro.py [wav-dir]

Writes nitro-engage.pcm, nitro-loop.pcm and nitro-release.pcm next to this script (the format HornShop.Pcm reads);
with a folder argument also the same three as .wav for listening. Everything is shaped noise and sines from a fixed
seed, so a run gives the same files again. Needs numpy.
"""
import os
import sys
import wave

import numpy as np

RATE = 22050
PEAK = 10 ** (-1.5 / 20)  # -1.5 dBFS, under the -1 dBFS ceiling
rng = np.random.default_rng(2026)


def seconds(n):
    return int(round(n * RATE))


def band(x, lo, hi, soft=0.25):
    """Band-pass by masking the spectrum, with soft edges. Circular, so a periodic input stays periodic."""
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / RATE)
    mask = np.ones_like(f)
    if lo > 0:
        mask *= 1 / (1 + (lo / np.maximum(f, 1e-3)) ** (2 / soft))
    if hi < RATE / 2:
        mask *= 1 / (1 + (f / hi) ** (2 / soft))
    return np.fft.irfft(spec * mask, len(x))


def sweep(x, centre, width):
    """
    Band-pass whose centre moves over time: short Hann frames (75 % overlap), each masked around its own centre,
    added back together. centre(t) and width(t) are in Hz and take time in seconds.
    """
    n, hop = 1024, 256
    win = np.hanning(n)
    out = np.zeros(len(x) + n)
    norm = np.zeros(len(x) + n)
    padded = np.concatenate([x, np.zeros(n)])
    f = np.fft.rfftfreq(n, 1 / RATE)
    for start in range(0, len(x), hop):
        t = (start + n / 2) / RATE
        c, w = centre(t), width(t)
        mask = np.exp(-0.5 * ((f - c) / w) ** 2)
        frame = np.fft.irfft(np.fft.rfft(padded[start:start + n] * win) * mask, n)
        out[start:start + n] += frame * win
        norm[start:start + n] += win ** 2
    return out[:len(x)] / np.maximum(norm[:len(x)], 1e-6)


def smooth_noise(n, rate_hz):
    """Slow random wobble around 0, roughly rate_hz changes per second."""
    return band(rng.standard_normal(n), 0, rate_hz, soft=0.5)


def norm(x, peak=PEAK):
    return x * (peak / np.max(np.abs(x)))


def engage():
    """A hard 'psssh' of the valve opening, then a whoosh whose pitch climbs as the gas rushes in."""
    n = seconds(0.62)
    t = np.arange(n) / RATE
    noise = rng.standard_normal(n)
    # The valve: bright hiss with a 4 ms attack that dies in about 0.15 s.
    psssh = band(noise, 3500, 9500) * np.minimum(t / 0.004, 1) * np.exp(-t / 0.07)
    # The whoosh: a band climbing from 700 Hz to 4.5 kHz, swelling in and fading at the end.
    whoosh = sweep(rng.standard_normal(n), lambda s: 700 + 3800 * (s / 0.62) ** 0.8, lambda s: 350 + 900 * s / 0.62)
    whoosh *= np.minimum(t / 0.05, 1) * np.clip((0.62 - t) / 0.2, 0, 1) ** 1.5
    # A dull thump under the start, felt more than heard.
    thump = np.sin(2 * np.pi * (70 - 25 * t) * t) * np.exp(-t / 0.08) * np.minimum(t / 0.003, 1)
    out = 1.0 * norm(psssh) + 0.75 * norm(whoosh) + 0.35 * thump
    fade = np.clip((0.62 - t) / 0.03, 0, 1)
    return norm(out * fade)


def loop():
    """
    The boost held: rushing air in a band that drifts and churns, over a low rumble. Built 0.25 s longer than the
    loop, then the overhang is crossfaded (equal power) into the start, so the last sample runs into the first.
    """
    length, over = 2.0, 0.25
    n, x = seconds(length), seconds(over)
    total = n + x
    t = np.arange(total) / RATE
    air = sweep(rng.standard_normal(total), lambda s: 2600 + 700 * np.sin(2 * np.pi * 0.9 * s) + 300 * np.sin(2 * np.pi * 2.3 * s),
                lambda s: 1300)
    hiss = band(rng.standard_normal(total), 5000, 9000)
    churn = 1 + 0.35 * smooth_noise(total, 9) / np.std(smooth_noise(total, 9))
    air = norm(air) * np.clip(churn, 0.3, 1.8) + 0.25 * norm(hiss)
    rumble_noise = band(rng.standard_normal(total), 25, 140, soft=0.4)
    rumble = norm(rumble_noise) * 0.7 + 0.3 * np.sin(2 * np.pi * 48 * t) * (1 + 0.3 * np.sin(2 * np.pi * 6 * t))
    out = 0.75 * norm(air) + 0.5 * norm(rumble)
    head, tail = out[:x], out[n:]
    ramp = np.linspace(0, np.pi / 2, x)
    body = out[:n].copy()
    body[:x] = head * np.sin(ramp) + tail * np.cos(ramp)
    return norm(body)


def release():
    """The valve closing: a hiss that drops in pitch and dies away, chopped into a turbo-like flutter."""
    n = seconds(0.5)
    t = np.arange(n) / RATE
    hiss = sweep(rng.standard_normal(n), lambda s: 4200 - 2600 * (s / 0.5) ** 0.7, lambda s: 900 - 400 * s / 0.5)
    # Flutter slowing from 26 to 14 beats per second, deep at first and shallower as it fades.
    rate = 26 - 12 * t / 0.5
    phase = 2 * np.pi * np.cumsum(rate) / RATE
    flutter = 1 - (0.75 - 0.4 * t / 0.5) * (0.5 + 0.5 * np.cos(phase))
    env = np.minimum(t / 0.006, 1) * np.exp(-t / 0.14) * np.clip((0.5 - t) / 0.04, 0, 1)
    air = band(rng.standard_normal(n), 6000, 10000) * np.exp(-t / 0.05)
    return norm(norm(hiss) * flutter * env + 0.3 * norm(air) * np.minimum(t / 0.004, 1))


def pcm(x):
    return np.round(np.clip(x, -1, 1) * 32767).astype("<i2").tobytes()


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    wav_dir = sys.argv[1] if len(sys.argv) > 1 else None
    if wav_dir:
        os.makedirs(wav_dir, exist_ok=True)
    for name, make in (("nitro-engage", engage), ("nitro-loop", loop), ("nitro-release", release)):
        data = pcm(make())
        with open(os.path.join(here, name + ".pcm"), "wb") as f:
            f.write(data)
        samples = np.frombuffer(data, "<i2").astype(np.float64) / 32768
        peak_db = 20 * np.log10(np.max(np.abs(samples)))
        line = "%s: %d samples, %.3f s, peak %.2f dBFS" % (name, len(samples), len(samples) / RATE, peak_db)
        if name == "nitro-loop":
            seam = abs(samples[0] - samples[-1])
            typical = np.median(np.abs(np.diff(samples)))
            line += ", seam step %.4f (median step %.4f)" % (seam, typical)
        print(line)
        if wav_dir:
            with wave.open(os.path.join(wav_dir, name + ".wav"), "wb") as w:
                w.setnchannels(1)
                w.setsampwidth(2)
                w.setframerate(RATE)
                w.writeframes(data)


if __name__ == "__main__":
    main()

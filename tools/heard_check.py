"""How far what the reader takes from one kind of file is from what it takes from another (D3).

    python tools/heard_check.py <folder of *.heard.wav> <the file to compare with, e.g. song.wav>

`dettato audio <folder> --out <folder>` writes the .heard.wav. For each other file of the same rate: how many
samples later the same sound comes (the peak of the cross-correlation over the first 30 seconds), and how much
of it differs once that is taken out (the difference's energy against the sound's, in dB).
"""
import sys
from pathlib import Path

import numpy as np
import soundfile as sf


def lag(a, b, most=8192):
    """b is a, this many samples later."""
    n = 1 << 21
    A, B = np.fft.rfft(a[:n], 2 * n), np.fft.rfft(b[:n], 2 * n)
    c = np.fft.irfft(np.conj(A) * B)
    c = np.concatenate([c[-most:], c[:most + 1]])
    return int(np.argmax(c)) - most


folder, name = Path(sys.argv[1]), sys.argv[2]
ref, rate = sf.read(folder / f"{name}.heard.wav")
for path in sorted(folder.glob("*.heard.wav")):
    x, r = sf.read(path)
    if path.stem == f"{name}.heard" or r != rate:
        continue
    d = lag(ref[:30 * rate], x[:30 * rate])
    y = x[d:] if d >= 0 else np.concatenate([np.zeros(-d), x])
    m = min(len(ref), len(y))
    off = 10 * np.log10(np.sum((ref[:m] - y[:m]) ** 2) / np.sum(ref[:m] ** 2))
    print(f"{path.stem[:-6]:12} {d:+6d} samples ({1000 * d / rate:+6.1f} ms), {len(x) - len(ref):+6d} longer, differs {off:6.1f} dB")

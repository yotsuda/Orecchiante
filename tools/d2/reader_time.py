"""D2: where read_chords.py --ears spends its time on one recording."""
import sys
import time

clock = time.perf_counter()
sys.path.insert(0, "tools")
import librosa
import numpy as np
import read_chords as rc
print(f"import librosa, numpy, read_chords: {time.perf_counter() - clock:.2f} s")
path = sys.argv[1]
for turn in (1, 2):
    clock = time.perf_counter()
    y, _ = librosa.load(path, sr=rc.SR, mono=True)
    took = {"load, to %d Hz" % rc.SR: time.perf_counter() - clock}
    clock = time.perf_counter()
    harmonic = librosa.effects.harmonic(y, kernel_size=(rc.HARMONIC_FRAMES, 31), margin=4)
    took["harmonic part (HPSS)"] = time.perf_counter() - clock
    clock = time.perf_counter()
    librosa.onset.onset_strength(y=y, sr=rc.SR, hop_length=rc.HOP)
    took["onset strength"] = time.perf_counter() - clock
    clock = time.perf_counter()
    librosa.feature.chroma_cqt(y=harmonic, sr=rc.SR, hop_length=rc.HOP, fmin=librosa.note_to_hz("C2"), n_octaves=5)
    librosa.feature.chroma_cqt(y=harmonic, sr=rc.SR, hop_length=rc.HOP, fmin=librosa.note_to_hz("C1"), n_octaves=2)
    took["two chromas"] = time.perf_counter() - clock
    clock = time.perf_counter()
    librosa.feature.rms(y=y, hop_length=rc.HOP)
    took["rms"] = time.perf_counter() - clock
    print(f"turn {turn}: " + ", ".join(f"{k} {v:.2f} s" for k, v in took.items()) + f"; in all {sum(took.values()):.2f} s")
rc.use_ears()
f = rc.features(path)
ears = sys.argv[2]
from pathlib import Path
stem = Path(path).stem
clock = time.perf_counter()
lines, beats = rc.lines_of(Path(ears) / (stem + ".beats"))
result = rc.read(path, f=f, lines=lines, beats=beats, heard=np.load(Path(ears) / (stem + ".npz")))
text = rc.lys(result, stem)
print(f"putting the chart together (read and lys), features given: {time.perf_counter() - clock:.2f} s; SR {rc.SR}, HOP {rc.HOP}")

#!/usr/bin/env python3
"""The chords of recordings as crema's chord model hears them (McFee & Bello, ISMIR 2017; BSD
2-Clause, and its author counts the weights as part of the software: bmcfee/crema issue 42): a
candidate for the ears of read_chords.py, measured beside BTC.

    .venv-crema/Scripts/python tools/crema_chords.py scratch/real/rwc-p --out scratch/real/chords-crema

For each .wav it writes <name>.lab and <name>.npz in the form btc_chords.py --probs writes: logp, the
log probability of each of the 170 chords in each frame (root * 14 + quality, then X and N, the order
of BTC), and step, a frame's seconds. Only the model's chord_tag output is used (not its root, bass
and pitch outputs, nor its own decoding).

crema 0.2.0 was written for Keras 2: this runs it on tf-keras (README), and takes the CQT from its
pump alone, because the rest of the pump no longer runs on the scikit-learn of today.
"""
import argparse
import os
import sys
import time
import warnings
from pathlib import Path

import numpy as np

os.environ.setdefault("TF_USE_LEGACY_KERAS", "1")
os.environ.setdefault("TF_CPP_MIN_LOG_LEVEL", "2")
warnings.filterwarnings("ignore")

# the order of btc_chords.py (BTC's idx2voca_chord)
ROOTS = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]
QUALITIES = ["min", "maj", "dim", "aug", "min6", "maj6", "min7", "minmaj7", "maj7", "7", "dim7", "hdim7", "sus2", "sus4"]
FLATS = {"Db": "C#", "Eb": "D#", "Gb": "F#", "Ab": "G#", "Bb": "A#", "Cb": "B", "Fb": "E"}


def main():
    p = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    p.add_argument("input", type=Path, help="a .wav, or a folder of them")
    p.add_argument("--out", type=Path, required=True)
    args = p.parse_args()
    import tf_keras
    sys.modules["keras"] = tf_keras
    for part in ("models", "layers", "backend", "utils"):
        sys.modules["keras." + part] = getattr(tf_keras, part)
    import librosa
    from crema.models.chord import ChordModel

    crema = ChordModel()
    cqt = next(op for op in crema.pump.ops if op.name == "cqt")
    tags = [str(c) for c in crema.pump["chord_tag"].encoder.classes_]
    names = [f"{r}:{q}" for r in ROOTS for q in QUALITIES] + ["X", "N"]
    where = []
    for tag in tags:                       # the place of each of crema's classes in BTC's order
        root, _, quality = tag.partition(":")
        where.append(names.index(tag if tag in ("X", "N") else f"{FLATS.get(root, root)}:{quality or 'maj'}"))
    assert sorted(where) == list(range(170)), "crema's chord_tag classes are not the 170 of BTC"
    out_at = crema.model.output_names.index("chord_tag")
    step = cqt.hop_length / cqt.sr
    print(f"crema {crema.version}: {crema.model.count_params():,} parameters, {len(tags)} classes, a frame {step:.4f} s", flush=True)

    files = sorted(args.input.glob("*.wav")) if args.input.is_dir() else [args.input]
    args.out.mkdir(parents=True, exist_ok=True)
    spent = {"features": 0.0, "model": 0.0}
    for path in files:
        target = args.out / (path.stem + ".lab")
        heard = args.out / (path.stem + ".npz")
        if target.exists() and heard.exists():
            continue
        clock = time.perf_counter()
        audio, rate = librosa.load(str(path), sr=cqt.sr)
        mag = cqt.transform(audio, rate)[cqt.scope("mag")]
        spent["features"] += time.perf_counter() - clock
        clock = time.perf_counter()
        prob = crema.model.predict(mag, verbose=0)[out_at][0]
        logp = np.full_like(prob, -np.inf, dtype=np.float64)
        logp[:, where] = np.log(np.maximum(prob, 1e-12))
        spent["model"] += time.perf_counter() - clock
        said = logp.argmax(-1)
        np.savez_compressed(heard, logp=logp.astype(np.float16), step=np.float64(step))
        lines, start = [], 0
        for i in range(1, len(said) + 1):
            if i == len(said) or said[i] != said[start]:
                lines.append(f"{start * step:.3f} {i * step:.3f} {names[int(said[start])]}")
                start = i
        target.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
        print(f"{path.name}: {len(lines)} chords", flush=True)
    print(f"features {spent['features']:.1f} s, model {spent['model']:.1f} s", flush=True)


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Learns what a chord sounds like, for read_chords.py to use in place of its templates.

    .venv/Scripts/python tools/train_chords.py data/synth/seed100 --out models/chords.joblib

What is learnt is small and local: from the chroma of one half beat (twelve numbers for the
notes above the bass, twelve for the bass) to the chord sounding then. Where the bar lines are,
when a chord may change and which key it all is in stay rules in read_chords.py: the model is
asked what it hears, not how music goes.

Every half beat is learnt in all twelve keys (the chroma turned, the chord's root with it), so
the model cannot prefer a key. The half beats are the reader's own (its beat tracker's), each
given the chord the truth has at its middle.
"""
import argparse
import json
import sys
from pathlib import Path

import joblib
import numpy as np
from sklearn.neural_network import MLPClassifier

import read_chords as rc
import score_chords as sc

CHORD_INDEX = {chord: i for i, chord in enumerate(rc.CHORDS)}


def units_of(f):
    """The reader's half beats over a recording: (treble chroma, bass chroma, middle in seconds)."""
    beats, _ = rc.beat_grid(f["oenv"])
    edges = np.interp(np.arange(0, len(beats) - 1 + 1e-9, 0.5), np.arange(len(beats)), beats)
    pairs = list(zip(edges[:-1], edges[1:]))
    treble = rc.unit(np.array([rc.over(f["chroma"], a, b) for a, b in pairs]).T)
    low = rc.unit(np.array([rc.over(f["bass"], a, b) for a, b in pairs]).T)
    middle = np.array([((a + b) / 2 - rc.BEAT_LAG) * rc.HOP / rc.SR for a, b in pairs])
    return treble, low, middle


def examples(truth, f):
    treble, low, middle = units_of(f)
    spans = [(c["start"], c["end"], sc.parse(c["symbol"])) for bar in truth["bars"] for c in bar["chords"]]
    xs, ys = [], []
    for i, at in enumerate(middle):
        chord = next(((root, quality) for start, end, (root, quality, _) in spans if start <= at < end), None)
        if chord is None:
            continue
        for turn in range(12):
            xs.append(np.concatenate([np.roll(treble[:, i], turn), np.roll(low[:, i], turn)]))
            ys.append(CHORD_INDEX[((chord[0] + turn) % 12, chord[1])])
    return xs, ys


def main():
    p = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    p.add_argument("truth", type=Path, help="a folder of gen_chords.py's songs, rendered")
    p.add_argument("--out", type=Path, required=True)
    p.add_argument("--features", type=Path, help="a folder of features kept as <audio name>.npz")
    p.add_argument("--hidden", type=int, default=128)
    args = p.parse_args()
    xs, ys = [], []
    for path in sorted(args.truth.glob("*.truth.json")):
        truth = json.loads(path.read_text(encoding="utf-8"))
        wav = path.parent / truth["audio"]
        kept = args.features / (wav.stem + ".npz") if args.features else None
        f = dict(np.load(kept)) if kept and kept.exists() else rc.features(wav)
        x, y = examples(truth, f)
        xs += x
        ys += y
    x, y = np.array(xs, dtype=np.float32), np.array(ys)
    print(f"{len(y)} half beats (each in twelve keys), {len(set(ys))} chords", flush=True)
    model = MLPClassifier(hidden_layer_sizes=(args.hidden,), max_iter=40, early_stopping=True,
                          validation_fraction=0.05, random_state=1, verbose=True)
    model.fit(x, y)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    joblib.dump(model, args.out)
    print(f"wrote {args.out}: validation {model.best_validation_score_:.3f}")


if __name__ == "__main__":
    sys.exit(main())

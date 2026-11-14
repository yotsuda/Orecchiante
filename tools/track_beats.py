#!/usr/bin/env python3
"""Beats and bar lines of recordings from Beat This! (CPJKU, MIT: code and weights), a model
trained on recordings, written one file a recording for measure.py --lines.

    .venv-bt/Scripts/python tools/track_beats.py scratch/real/rwc-p --out scratch/real/beats-bt-folds --rwc scratch/real
    .venv-bt/Scripts/python tools/track_beats.py some/folder --out some/beats            # the model for use: final0

It runs in a virtual environment of its own (torch; see the README). <recording>.beats has a
line a beat: its seconds, a tab, its number in the bar (1 is a bar line).

`--rwc <folder>` is for measuring on RWC, which the model for use was trained on: each song is
then read by the one of the eight cross-validation models that never heard it. The folder holds
clones of rwc-annotations (metadata.csv: which CD and track a song is) and of
beat_this_annotations (rwc/8-folds.split: which fold held it out).
"""
import argparse
import csv
import sys
from pathlib import Path

import numpy as np


def folds_of(rwc):
    """RWC_P001 -> the fold whose model did not train on it."""
    fold = {}
    for line in (rwc / "beat_this_annotations" / "rwc" / "8-folds.split").read_text().splitlines():
        name, _, n = line.partition("\t")
        if name.strip():
            fold[name.strip()] = int(n)
    out = {}
    with open(rwc / "rwc-annotations" / "metadata.csv", encoding="utf-8", newline="") as f:
        for m in csv.DictReader(f, delimiter=";"):
            if m["CollID"] != "P":
                continue
            key = f"rwc_popular_CD{int(float(m['CDNo']))}_{int(float(m['TrackNo'])):02d}"
            if key in fold:
                out[m["RWCID"]] = fold[key]
    return out


def main():
    p = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    p.add_argument("input", type=Path, help="a .wav, or a folder of them")
    p.add_argument("--out", type=Path, required=True)
    p.add_argument("--model", default="final0")
    p.add_argument("--rwc", type=Path, help="measure RWC fairly: the folder of the two annotation clones")
    args = p.parse_args()
    from beat_this.inference import File2Beats

    files = sorted(args.input.glob("*.wav")) if args.input.is_dir() else [args.input]
    folds = folds_of(args.rwc) if args.rwc else {}
    if args.rwc:
        missing = [f.stem for f in files if f.stem not in folds]
        if missing:
            sys.exit(f"no fold known for {len(missing)} songs: {missing[:5]}")
    args.out.mkdir(parents=True, exist_ok=True)
    models = {}
    for path in files:
        target = args.out / (path.stem + ".beats")
        if target.exists():
            continue
        name = f"fold{folds[path.stem]}" if folds else args.model
        if name not in models:
            models[name] = File2Beats(checkpoint_path=name, device="cpu", dbn=False)
        beats, downbeats = models[name](str(path))
        beats, downbeats = np.asarray(beats, dtype=float), np.asarray(downbeats, dtype=float)
        lines, number = [], 0
        for t in beats:
            down = len(downbeats) and float(np.min(np.abs(downbeats - t))) < 0.035
            number = 1 if down else number + 1 if number else 0    # beats before the first bar line are 0
            lines.append(f"{t:.3f}\t{number}")
        target.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
        print(f"{path.name}: {len(beats)} beats, {len(downbeats)} bar lines ({name})", flush=True)


if __name__ == "__main__":
    main()

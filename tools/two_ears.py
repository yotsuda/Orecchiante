#!/usr/bin/env python3
"""Two ears for the chords (the owner, 2026-11-16): crema's and BTC's probabilities, averaged, BTC's frames brought to
crema's (each crema frame takes the BTC frame its middle falls in). What orecchiante does inside (Btc.cs, Program.cs);
the files are what read_chords.py --ears and measure.py --ears crema+btc read.

    py -3.12 tools/two_ears.py scratch/chords-crema/seed1 scratch/chords-btc/seed1 --out scratch/chords-crema+btc/seed1

Each folder has <name>.npz of logp (frames x 170, float16) and step (a frame's seconds).
"""
import argparse
from pathlib import Path

import numpy as np


def average(a, step_a, b, step_b):
    """log of the mean of the two probabilities, on a's frames; float16 as the files hold them."""
    p, q = np.exp(a.astype(float)), np.exp(b.astype(float))
    middle = (np.arange(len(p)) + 0.5) * step_a
    q = q[np.clip((middle / step_b).astype(int), 0, len(q) - 1)]
    return np.log(np.maximum((p + q) / 2, 1e-12)).astype(np.float16)


def main():
    p = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    p.add_argument("first", type=Path, help="crema's <name>.npz")
    p.add_argument("second", type=Path, help="BTC's <name>.npz")
    p.add_argument("--out", type=Path, required=True)
    args = p.parse_args()
    args.out.mkdir(parents=True, exist_ok=True)
    for f in sorted(args.first.glob("*.npz")):
        x, y = np.load(f), np.load(args.second / f.name)
        np.savez_compressed(args.out / f.name, logp=average(x["logp"], float(x["step"]), y["logp"], float(y["step"])),
                            step=x["step"])


if __name__ == "__main__":
    main()

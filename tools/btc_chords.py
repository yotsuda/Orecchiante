#!/usr/bin/env python3
"""The chords of recordings as BTC hears them (Park et al., ISMIR 2019; MIT), a model trained on
recordings: a yardstick for the chord model of this project, not a part of it.

    .venv-bt/Scripts/python tools/btc_chords.py <BTC-ISMIR19 clone> scratch/real/rwc-p --out scratch/real/chords-btc

For each .wav it writes <name>.lab: start, end and chord label (Harte's syntax, 170 chords) on
each line; with --probs, <name>.npz too, what read_chords.py puts chords together from. It is BTC's own test.py without the MIDI, made to run with the libraries of today.
Its README names Isophonics, Robbie Williams and USPop2002 as the training data (not RWC).
"""
import argparse
import sys
from pathlib import Path

import numpy as np
import torch
import yaml


def main():
    p = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    p.add_argument("btc", type=Path, help="a clone of https://github.com/jayg996/BTC-ISMIR19")
    p.add_argument("input", type=Path, help="a .wav, or a folder of them")
    p.add_argument("--out", type=Path, required=True)
    p.add_argument("--probs", action="store_true",
                   help="also write <name>.npz: logp, the log probability of each of the 170 chords in each frame "
                        "(root * 14 + quality, then X and N), and step, a frame's seconds")
    args = p.parse_args()
    np.float = float                      # BTC was written for a numpy that still had this name
    sys.path.insert(0, str(args.btc))
    from btc_model import BTC_model
    from utils.hparams import HParams
    from utils.mir_eval_modules import audio_file_to_features, idx2voca_chord

    config = HParams(**yaml.safe_load((args.btc / "run_config.yaml").read_text()))
    config.feature["large_voca"] = True
    config.model["num_chords"] = 170
    names = idx2voca_chord()
    model = BTC_model(config=config.model)
    # the mean and the deviation are saved as numpy scalars, under the names numpy had then
    from numpy._core import multiarray
    with torch.serialization.safe_globals([(multiarray.scalar, "numpy.core.multiarray.scalar"),
                                           (multiarray._reconstruct, "numpy.core.multiarray._reconstruct"),
                                           np.ndarray, np.dtype, type(np.dtype("float64")), type(np.dtype("float32"))]):
        saved = torch.load(args.btc / "test" / "btc_model_large_voca.pt", map_location="cpu", weights_only=True)
    mean, std = saved["mean"], saved["std"]
    model.load_state_dict(saved["model"])
    model.eval()

    files = sorted(args.input.glob("*.wav")) if args.input.is_dir() else [args.input]
    args.out.mkdir(parents=True, exist_ok=True)
    steps = config.model["timestep"]
    for path in files:
        target = args.out / (path.stem + ".lab")
        heard = args.out / (path.stem + ".npz")
        if target.exists() and (heard.exists() or not args.probs):
            continue
        feature, per_second, _ = audio_file_to_features(str(path), config)
        feature = (feature.T - mean) / std
        pad = steps - feature.shape[0] % steps
        feature = np.pad(feature, ((0, pad), (0, 0)), mode="constant", constant_values=0)
        said, logp = [], []
        with torch.no_grad():
            x = torch.tensor(feature, dtype=torch.float32).unsqueeze(0)
            for t in range(feature.shape[0] // steps):
                hidden, _ = model.self_attn_layers(x[:, steps * t:steps * (t + 1), :])
                logits = model.output_layer.output_projection(hidden)
                logp.append(torch.log_softmax(logits, -1).squeeze(0).numpy())
                said += logits.argmax(-1).squeeze().tolist()
        said = said[:len(said) - pad]
        if args.probs:
            np.savez_compressed(heard, logp=np.concatenate(logp)[:len(said)].astype(np.float16),
                                step=np.float64(per_second))
        lines, start = [], 0
        for i in range(1, len(said) + 1):
            if i == len(said) or said[i] != said[start]:
                lines.append(f"{start * per_second:.3f} {i * per_second:.3f} {names[said[start]]}")
                start = i
        target.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
        print(f"{path.name}: {len(lines)} chords", flush=True)


if __name__ == "__main__":
    main()

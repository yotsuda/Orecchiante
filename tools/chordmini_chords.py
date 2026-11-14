#!/usr/bin/env python3
"""The chords of recordings as the ChordMini models hear them (Phan et al., DAFx 2026; MIT repository
with the checkpoints in it): candidates for the ears of read_chords.py, measured beside BTC.

    .venv-bt/Scripts/python tools/chordmini_chords.py <ChordMini clone> scratch/real/rwc-p --model 2e1d --out scratch/real/chords-cm2e1d

For each .wav it writes <name>.lab and <name>.npz in the form btc_chords.py --probs writes: logp, the
log probability of each of the 170 chords in each frame (root * 14 + quality, then X and N), and
step, a frame's seconds. Windows of 108 frames, half overlapping, their logits averaged.

Their paper names Isophonics, McGill Billboard, RWC Pop and USPop as the labelled songs (600 of
them, 420 for training; which ones is not published), so numbers on RWC flatter these models.
"""
import argparse
import sys
import time
from pathlib import Path

import numpy as np
import torch

MODELS = {"2e1d": ("ChordNet", "2e1d_model_best.pth"), "btc": ("BTC", "btc_model_best.pth")}
WINDOW = 108


def main():
    p = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    p.add_argument("clone", type=Path, help="a clone of https://github.com/ptnghia-j/ChordMini")
    p.add_argument("input", type=Path, help="a .wav, or a folder of them")
    p.add_argument("--model", choices=sorted(MODELS), default="2e1d")
    p.add_argument("--out", type=Path, required=True)
    args = p.parse_args()
    sys.path.insert(0, str(args.clone))
    sys.path.insert(0, str(args.clone / "src"))
    import librosa
    from src.models import load_model
    from src.utils import HParams, idx2voca_chord

    kind, file = MODELS[args.model]
    config = HParams.load(str(args.clone / "config" / "ChordMini.yaml"))
    model, mean, std = load_model(str(args.clone / "checkpoints" / file), kind, config, torch.device("cpu"))
    model.eval()
    mean = torch.as_tensor(mean, dtype=torch.float32)
    std = torch.as_tensor(std, dtype=torch.float32)
    names = idx2voca_chord()
    print(f"{file}: {sum(p.numel() for p in model.parameters()):,} parameters", flush=True)

    files = sorted(args.input.glob("*.wav")) if args.input.is_dir() else [args.input]
    args.out.mkdir(parents=True, exist_ok=True)
    spent = {"features": 0.0, "model": 0.0}
    for path in files:
        target = args.out / (path.stem + ".lab")
        heard = args.out / (path.stem + ".npz")
        if target.exists() and heard.exists():
            continue
        clock = time.perf_counter()
        # their extract_song_features (src/evaluation/utils/common.py), which cannot be imported without matplotlib
        audio, rate = librosa.load(str(path), sr=22050)
        cqt = librosa.cqt(audio, sr=rate, n_bins=144, bins_per_octave=24, hop_length=2048,
                          fmin=librosa.note_to_hz("C1"))
        feature, step = np.log(np.abs(cqt) + 1e-6).T.astype(np.float32), 2048 / rate
        spent["features"] += time.perf_counter() - clock
        clock = time.perf_counter()
        frames = feature.shape[0]
        pad = -frames % WINDOW
        feature = np.pad(feature, ((0, pad), (0, 0)), mode="constant")
        starts = list(range(0, feature.shape[0] - WINDOW + 1, WINDOW // 2))
        total = np.zeros((feature.shape[0], len(names)), dtype=np.float64)
        count = np.zeros(feature.shape[0])
        with torch.no_grad():
            for at in range(0, len(starts), 16):
                x = torch.from_numpy(np.stack([feature[s:s + WINDOW] for s in starts[at:at + 16]])).float()
                out = model((x - mean) / (std + 1e-8))
                logits = (out[0] if isinstance(out, tuple) else out).numpy()
                for s, one in zip(starts[at:at + 16], logits):
                    total[s:s + WINDOW] += one
                    count[s:s + WINDOW] += 1
        logits = torch.from_numpy((total / count[:, None])[:frames])
        logp = torch.log_softmax(logits, -1).numpy()
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

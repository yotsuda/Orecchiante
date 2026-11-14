"""D2: the log mel spectrogram torch makes for Beat This!, saved for ears_onnx.py --mel-against.

    .venv-bt/Scripts/python tools/d2/bt_mel.py data/synth/seed1 --out scratch/d2/mel/seed1 [--songs 5]
"""
import argparse
from pathlib import Path

import numpy as np
from beat_this.inference import Audio2Frames
from beat_this.preprocessing import load_audio

p = argparse.ArgumentParser()
p.add_argument("input", type=Path)
p.add_argument("--out", type=Path, required=True)
p.add_argument("--songs", type=int)
args = p.parse_args()
ears = Audio2Frames(checkpoint_path="final0", device="cpu")
args.out.mkdir(parents=True, exist_ok=True)
files = sorted(args.input.glob("*.wav"))
for path in files[: args.songs] if args.songs else files:
    signal, rate = load_audio(str(path))
    np.save(args.out / (path.stem + ".mel.npy"), ears.signal2spect(signal, rate).numpy())
print(len(files), "done")

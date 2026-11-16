"""BTC (Park et al., ISMIR 2019; MIT) as ONNX, the second ear (the owner, 2026-11-16): can torch write it, does
onnxruntime say what torch says?

    .venv-bt/Scripts/python tools/d2/btc_onnx.py scratch/btc scratch/real/rwc-p --songs 10

The graph takes one window of 108 frames of BTC's feature as it is (log |CQT| + 1e-6: 22050 Hz, 144 bins from C1,
24 to the octave, every 2048 samples, cut in 10-second pieces), normalises it with the mean and the deviation saved
with the weights, and gives log p of each of the 170 chords (root * 14 + quality, then X and N), [1, 108, 170].
Writes scratch/d2/btc-large-voca.onnx.
"""
import argparse
import sys
from pathlib import Path

import numpy as np
import torch
import yaml

p = argparse.ArgumentParser()
p.add_argument("btc", type=Path, help="a clone of https://github.com/jayg996/BTC-ISMIR19")
p.add_argument("input", type=Path)
p.add_argument("--songs", type=int, default=10)
p.add_argument("--onnx", type=Path, default=Path("scratch/d2/btc-large-voca.onnx"))
args = p.parse_args()

np.float = float
sys.path.insert(0, str(args.btc))
from btc_model import BTC_model                                          # noqa: E402
from utils.hparams import HParams                                        # noqa: E402
from utils.mir_eval_modules import audio_file_to_features               # noqa: E402

config = HParams(**yaml.safe_load((args.btc / "run_config.yaml").read_text()))
config.feature["large_voca"] = True
config.model["num_chords"] = 170
model = BTC_model(config=config.model)
from numpy._core import multiarray                                       # noqa: E402
with torch.serialization.safe_globals([(multiarray.scalar, "numpy.core.multiarray.scalar"),
                                       (multiarray._reconstruct, "numpy.core.multiarray._reconstruct"),
                                       np.ndarray, np.dtype, type(np.dtype("float64")), type(np.dtype("float32"))]):
    saved = torch.load(args.btc / "test" / "btc_model_large_voca.pt", map_location="cpu", weights_only=True)
mean, std = float(saved["mean"]), float(saved["std"])
model.load_state_dict(saved["model"])
model.eval()
steps = config.model["timestep"]
print(f"mean {mean!r}, std {std!r}, timestep {steps}, {sum(w.numel() for w in model.parameters()):,} parameters")


class Window(torch.nn.Module):
    def __init__(self, model):
        super().__init__()
        self.model = model

    def forward(self, feature):
        x = (feature - mean) / std
        hidden, _ = self.model.self_attn_layers(x)
        return torch.log_softmax(self.model.output_layer.output_projection(hidden), -1)


window = Window(model).eval()
if not args.onnx.exists():
    args.onnx.parent.mkdir(parents=True, exist_ok=True)
    with torch.no_grad():
        torch.onnx.export(window, (torch.randn(1, steps, 144),), str(args.onnx), dynamo=False, opset_version=17,
                          input_names=["feature"], output_names=["logp"])
    print(f"wrote {args.onnx}")
print(f"{args.onnx}: {args.onnx.stat().st_size / 1e6:.1f} MB")

import onnxruntime as ort                                                # noqa: E402
session = ort.InferenceSession(str(args.onnx), providers=["CPUExecutionProvider"])


def logp(feature, run):
    pad = steps - feature.shape[0] % steps
    x = np.pad(feature, ((0, pad), (0, 0)))
    out = np.concatenate([run(x[None, steps * t:steps * (t + 1)].astype(np.float32))[0]
                          for t in range(x.shape[0] // steps)])
    return out[:feature.shape[0]]


def by_torch(x):
    with torch.no_grad():
        return window(torch.from_numpy(x)).numpy()


worst, differ, frames = 0.0, 0, 0
for path in sorted(args.input.glob("*.wav"))[: args.songs]:
    feature, _, _ = audio_file_to_features(str(path), config)
    feature = feature.T
    a, b = logp(feature, by_torch), logp(feature, lambda x: session.run(None, {"feature": x})[0])
    gap = float(np.abs(a - b).max())
    worst = max(worst, gap)
    differ += int((a.argmax(1) != b.argmax(1)).sum())
    frames += len(a)
    print(f"{path.name}: {len(a)} frames, largest gap in log p {gap:.2e}, chords that differ {int((a.argmax(1) != b.argmax(1)).sum())}",
          flush=True)
print(f"largest gap {worst:.2e}; frames whose chord differs {differ}/{frames}")

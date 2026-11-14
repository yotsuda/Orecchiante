"""D2: Beat This! as ONNX. Can torch write it, does onnxruntime say what torch says, how fast?

    .venv-bt/Scripts/python tools/d2/bt_onnx.py scratch/real/rwc-p --songs 10 [--how trace|dynamo]
"""
import argparse
import time
from pathlib import Path

import numpy as np

p = argparse.ArgumentParser()
p.add_argument("input", type=Path)
p.add_argument("--songs", type=int, default=10)
p.add_argument("--how", default="trace")
p.add_argument("--model", default="final0")
p.add_argument("--onnx", type=Path)
args = p.parse_args()
target = args.onnx or Path(f"scratch/d2/beat-this-{args.model}-{args.how}.onnx")

clock = time.perf_counter()
import torch
from beat_this.inference import Audio2Frames, split_predict_aggregate
from beat_this.model.postprocessor import Postprocessor
from beat_this.preprocessing import load_audio
ears = Audio2Frames(checkpoint_path=args.model, device="cpu")
print(f"import torch + beat_this, load {args.model}: {time.perf_counter() - clock:.1f} s; "
      f"{sum(w.numel() for w in ears.model.parameters()):,} parameters", flush=True)


class Both(torch.nn.Module):
    def __init__(self, model):
        super().__init__()
        self.model = model

    def forward(self, spect):
        out = self.model(spect)
        return out["beat"], out["downbeat"]


if not target.exists():
    for m in ears.model.modules():
        if hasattr(m, "cache_if_possible"):
            m.cache_if_possible = False          # the rotary embedding keeps what it has made; a trace must not
    clock = time.perf_counter()
    example = torch.randn(1, 1500, 128)
    with torch.no_grad():
        if args.how == "trace":
            torch.onnx.export(Both(ears.model), (example,), str(target), dynamo=False, opset_version=17,
                              input_names=["spect"], output_names=["beat", "downbeat"],
                              dynamic_axes={"spect": {1: "frames"}, "beat": {1: "frames"}, "downbeat": {1: "frames"}})
        else:
            torch.onnx.export(Both(ears.model), (example,), str(target), dynamo=True,
                              input_names=["spect"], output_names=["beat", "downbeat"],
                              dynamic_shapes={"spect": {1: torch.export.Dim("frames", min=16, max=1500)}},
                              external_data=False)
    print(f"wrote {target} in {time.perf_counter() - clock:.1f} s", flush=True)
    # The model is loaded again to compare with: after the trace it answered otherwise in the same
    # process (465 beats of RWC_P001 for 460, six times slower), and the file written was right.
    ears = Audio2Frames(checkpoint_path=args.model, device="cpu")
print(f"{target}: {target.stat().st_size / 1e6:.1f} MB")

clock = time.perf_counter()
import onnx
import onnxruntime as ort
session = ort.InferenceSession(str(target), providers=["CPUExecutionProvider"])
print(f"import onnxruntime, open the session: {time.perf_counter() - clock:.2f} s")
print("onnx inputs:", [(i.name, i.shape) for i in session.get_inputs()], "outputs:", [(o.name, o.shape) for o in session.get_outputs()])
print("onnx ops:", sorted({n.op_type for n in onnx.load(str(target), load_external_data=False).graph.node}))


def by_onnx(chunk):
    beat, down = session.run(None, {"spect": chunk.numpy().astype(np.float32)})
    return {"beat": torch.from_numpy(beat), "downbeat": torch.from_numpy(down)}


def whole(spect, model):
    with torch.inference_mode():
        out = split_predict_aggregate(spect=spect, chunk_size=1500, overlap_mode="keep_first", border_size=6, model=model)
    return out["beat"].float(), out["downbeat"].float()


with torch.inference_mode():                      # a chunk shorter than the one it was written with
    short = torch.randn(1, 700, 128)
    a = ears.model(short)["beat"].numpy()
    print(f"700 frames: largest gap in the beat logit {np.abs(a - by_onnx(short)['beat'].numpy()).max():.2e}", flush=True)

peaks = Postprocessor(type="minimal")
files = sorted(args.input.glob("*.wav"))[: args.songs]
spent = {"load": 0.0, "mel": 0.0, "torch": 0.0, "ort": 0.0, "audio": 0.0}
worst, beats_all, beats_moved, downs_differ = 0.0, 0, 0, 0
for path in files:
    clock = time.perf_counter()
    signal, rate = load_audio(str(path))
    spent["load"] += time.perf_counter() - clock
    spent["audio"] += len(signal) / rate
    clock = time.perf_counter()
    spect = ears.signal2spect(signal, rate)
    spent["mel"] += time.perf_counter() - clock
    clock = time.perf_counter()
    a = whole(spect, ears.model)
    spent["torch"] += time.perf_counter() - clock
    clock = time.perf_counter()
    b = whole(spect, by_onnx)
    spent["ort"] += time.perf_counter() - clock
    gap = max(float((a[0] - b[0]).abs().max()), float((a[1] - b[1]).abs().max()))
    worst = max(worst, gap)
    (beat_a, down_a), (beat_b, down_b) = peaks(*a), peaks(*b)
    same = len(beat_a) == len(beat_b) and np.array_equal(beat_a, beat_b)
    beats_all += len(beat_a)
    beats_moved += 0 if same else len(set(np.round(beat_a, 3)) ^ set(np.round(beat_b, 3)))
    downs_differ += len(set(np.round(down_a, 3)) ^ set(np.round(down_b, 3)))
    print(f"{path.name}: spect {tuple(spect.shape)}, largest gap in a logit {gap:.2e}, beats {len(beat_a)} / {len(beat_b)}, "
          f"bar lines {len(down_a)} / {len(down_b)}", flush=True)
n = len(files)
print(f"{n} songs, {spent['audio'] / n:.0f} s of audio each; a song: load {spent['load'] / n:.2f} s, resample+mel {spent['mel'] / n:.2f} s, "
      f"torch {spent['torch'] / n:.2f} s, onnxruntime {spent['ort'] / n:.2f} s")
print(f"largest gap {worst:.2e}; beats that differ {beats_moved}/{beats_all}; bar lines that differ {downs_differ}")

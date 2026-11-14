"""D2: crema's chord model as ONNX. Does onnxruntime say what TensorFlow says, and how fast?

    .venv-crema/Scripts/python tools/d2/crema_onnx.py scratch/real/rwc-p --songs 10
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

p = argparse.ArgumentParser()
p.add_argument("input", type=Path)
p.add_argument("--songs", type=int, default=10)
p.add_argument("--onnx", type=Path, default=Path("scratch/d2/crema-chord.onnx"))
args = p.parse_args()

clock = time.perf_counter()
import tf_keras
sys.modules["keras"] = tf_keras
for part in ("models", "layers", "backend", "utils"):
    sys.modules["keras." + part] = getattr(tf_keras, part)
import tensorflow as tf
import librosa
from crema.models.chord import ChordModel
crema = ChordModel()
print(f"import tensorflow + crema, build the model: {time.perf_counter() - clock:.1f} s", flush=True)

cqt = next(op for op in crema.pump.ops if op.name == "cqt")
print("cqt:", {k: getattr(cqt, k) for k in ("sr", "hop_length", "n_octaves", "over_sample", "fmin", "log", "conv") if hasattr(cqt, k)},
      type(cqt).__name__, flush=True)
print("inputs:", [(i.name, i.shape) for i in crema.model.inputs])
print("outputs:", list(zip(crema.model.output_names, [o.shape for o in crema.model.outputs])))
print("layers:", sorted({type(l).__name__ for l in crema.model.layers}))
out_at = crema.model.output_names.index("chord_tag")

if not args.onnx.exists():
    import tf2onnx
    clock = time.perf_counter()
    spec = [tf.TensorSpec(i.shape, i.dtype, name=i.name.split(":")[0]) for i in crema.model.inputs]
    tf2onnx.convert.from_keras(crema.model, input_signature=spec, opset=17, output_path=str(args.onnx))
    print(f"wrote {args.onnx} in {time.perf_counter() - clock:.1f} s", flush=True)
print(f"{args.onnx}: {args.onnx.stat().st_size / 1e6:.1f} MB")

clock = time.perf_counter()
import onnxruntime as ort
session = ort.InferenceSession(str(args.onnx), providers=["CPUExecutionProvider"])
print(f"import onnxruntime, open the session: {time.perf_counter() - clock:.2f} s")
print("onnx inputs:", [(i.name, i.shape, i.type) for i in session.get_inputs()])
print("onnx outputs:", [(o.name, o.shape) for o in session.get_outputs()])
import onnx
print("onnx ops:", sorted({n.op_type for n in onnx.load(str(args.onnx)).graph.node}))
tag = next(o.name for o in session.get_outputs() if "chord_tag" in o.name)

files = sorted(args.input.glob("*.wav"))[: args.songs]
spent = {"load": 0.0, "cqt": 0.0, "tf": 0.0, "ort": 0.0, "audio": 0.0}
worst, differ, frames = 0.0, 0, 0
for path in files:
    clock = time.perf_counter()
    audio, rate = librosa.load(str(path), sr=cqt.sr)
    spent["load"] += time.perf_counter() - clock
    spent["audio"] += len(audio) / rate
    clock = time.perf_counter()
    mag = cqt.transform(audio, rate)[cqt.scope("mag")]
    spent["cqt"] += time.perf_counter() - clock
    clock = time.perf_counter()
    a = crema.model.predict(mag, verbose=0)[out_at][0]
    spent["tf"] += time.perf_counter() - clock
    clock = time.perf_counter()
    b = session.run([tag], {session.get_inputs()[0].name: mag.astype(np.float32)})[0][0]
    spent["ort"] += time.perf_counter() - clock
    gap = float(np.abs(a - b).max())
    worst = max(worst, gap)
    differ += int((a.argmax(-1) != b.argmax(-1)).sum())
    frames += len(a)
    print(f"{path.name}: mag {mag.shape} {mag.dtype}, largest gap in probability {gap:.2e}", flush=True)
n = len(files)
print(f"{n} songs, {spent['audio'] / n:.0f} s of audio each; a song: load+resample {spent['load'] / n:.2f} s, "
      f"cqt {spent['cqt'] / n:.2f} s, tensorflow {spent['tf'] / n:.2f} s, onnxruntime {spent['ort'] / n:.2f} s")
print(f"largest gap {worst:.2e}; frames whose best chord differs {differ}/{frames}")

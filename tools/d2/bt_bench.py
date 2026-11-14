"""D2: how fast onnxruntime runs a 30 s chunk of Beat This!, by threads and by how the file was written."""
import sys
import time
from pathlib import Path

import numpy as np
import onnxruntime as ort

x = np.random.default_rng(0).standard_normal((1, 1500, 128)).astype(np.float32)
for name in sys.argv[1:]:
    for threads in (4, 8, 12, 24):
        o = ort.SessionOptions()
        o.intra_op_num_threads = threads
        s = ort.InferenceSession(name, o, providers=["CPUExecutionProvider"])
        s.run(None, {"spect": x})
        clock = time.perf_counter()
        for _ in range(3):
            s.run(None, {"spect": x})
        print(f"{Path(name).name}: {threads} threads, {(time.perf_counter() - clock) / 3:.2f} s a chunk", flush=True)

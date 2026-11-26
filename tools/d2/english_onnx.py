"""facebook/wav2vec2-base-960h as ONNX for orecchiante, as tools/d2/reazon_onnx.py makes the Japanese one: the samples of
one chunk (16 kHz mono, raw) in, the log probabilities of the blank, the word break "|", the apostrophe and the letters
(as scratch/align/emit_en.py keeps them) out.
  scratch/align/venv/Scripts/python tools/d2/english_onnx.py [OUT=scratch/align/onnx]
Writes <OUT>/w2v-english.onnx (fp32) and w2v-english-mm8.onnx (MatMul int8, the one shipped), the tokens in the model's
metadata (`tokens`, in the output's column order after the blank).
"""
import json, sys
from pathlib import Path
import numpy as np

MODEL = "facebook/wav2vec2-base-960h"
OUT = Path("scratch/align/onnx")
for a in sys.argv[1:]:
    if a.startswith("OUT="):
        OUT = Path(a[4:])


def main():
    import torch
    from transformers import AutoModelForCTC
    from huggingface_hub import hf_hub_download
    vocab = json.load(open(hf_hub_download(MODEL, "vocab.json"), encoding="utf-8"))
    model = AutoModelForCTC.from_pretrained(MODEL).eval()
    blank = model.config.pad_token_id
    tokens = sorted(t for t in vocab if len(t) == 1 and (t.isalpha() or t in "'|"))
    index = torch.tensor([blank] + [vocab[t] for t in tokens])

    class Letters(torch.nn.Module):
        def __init__(self, m, index):
            super().__init__()
            self.m = m
            self.register_buffer("index", index)

        def forward(self, wave):                      # [1, N]
            x = (wave - wave.mean()) / torch.sqrt(wave.var(unbiased=False) + 1e-7)
            lp = torch.log_softmax(self.m(x).logits[0], dim=-1)          # [T, V]
            return lp[:, self.index]                                       # [T, 1 + K]

    net = Letters(model, index).eval()
    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / "w2v-english.onnx"
    x = torch.randn(1, 16000 * 24)
    torch.onnx.export(net, (x,), str(path), input_names=["wave"], output_names=["logp"],
                      dynamic_axes={"wave": {1: "samples"}, "logp": {0: "frames"}}, opset_version=17, dynamo=False)
    import onnx
    m = onnx.load(str(path))
    for k, v in (("tokens", json.dumps(tokens)), ("source", MODEL), ("license", "Apache-2.0"),
                 ("made_by", "tools/d2/english_onnx.py")):
        p = m.metadata_props.add(); p.key = k; p.value = v
    onnx.save(m, str(path))
    import onnxruntime as ort
    from onnxruntime.quantization import quantize_dynamic, QuantType
    q = OUT / "w2v-english-mm8.onnx"
    quantize_dynamic(str(path), str(q), weight_type=QuantType.QInt8, op_types_to_quantize=["MatMul"])
    y = torch.randn(1, 16000 * 17 + 123)
    with torch.no_grad():
        want = net(y).numpy()
    for p in (path, q):
        s = ort.InferenceSession(str(p), providers=["CPUExecutionProvider"])
        got = s.run(None, {"wave": y.numpy()})[0]
        print(f"{p.name}: {p.stat().st_size / 1e6:.1f} MB, frames {got.shape} vs {want.shape},"
              f" max |diff| {np.abs(got - want).max():.4f}, argmax agree {(got.argmax(1) == want.argmax(1)).mean():.4f}")


if __name__ == "__main__":
    main()

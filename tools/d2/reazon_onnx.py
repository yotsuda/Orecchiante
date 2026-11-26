"""reazon-research/japanese-wav2vec2-base-rs35kh as ONNX for orecchiante: the samples of one chunk (16 kHz mono, raw)
in, the log probabilities of the blank and the kana (as emit.py keeps them) out.
  scratch/align/venv/Scripts/python tools/d2/reazon_onnx.py [OUT=scratch/align/onnx]
(the venv: torch, transformers, huggingface_hub, onnx 1.23.2, onnxruntime 1.20.1 = the version orecchiante runs, onnxscript;
HF_HOME where the model is kept). The one shipped is reazon-kana-mm8.onnx (README "The words put to the voice").
Writes <OUT>/reazon-kana.onnx (fp32), reazon-kana-mm8.onnx (MatMul int8), reazon-kana-cv8.onnx (MatMul and Conv uint8)
and reazon-kana.json (the tokens, in the output's column order after the blank). In the graph: the chunk normalised as
the feature extractor does ((x - mean) / sqrt(var + 1e-7)), log_softmax over all 3003 tokens, then for each kana the
larger of its two tokens ("あ" and "▁あ").
"""
import json, os, sys
from pathlib import Path
import numpy as np

MODEL = "reazon-research/japanese-wav2vec2-base-rs35kh"
OUT = Path("scratch/align/onnx")
for a in sys.argv[1:]:
    if a.startswith("OUT="):
        OUT = Path(a[4:])


def kana_token(t):
    t = t.replace("▁", "")
    return len(t) == 1 and ("ぁ" <= t <= "ん" or t in "ーゔ")


def main():
    import torch
    from transformers import AutoModelForCTC
    from huggingface_hub import hf_hub_download
    vocab = json.load(open(hf_hub_download(MODEL, "vocab.json"), encoding="utf-8"))
    model = AutoModelForCTC.from_pretrained(MODEL).eval()
    blank = model.config.pad_token_id
    cols = {}
    for tok, i in vocab.items():
        if kana_token(tok):
            cols.setdefault(tok.replace("▁", ""), []).append(i)
    tokens = sorted(cols)
    width = max(len(v) for v in cols.values())
    index = torch.tensor([[blank] * width] + [(cols[k] * width)[:width] for k in tokens])   # [1 + K, width]

    class Kana(torch.nn.Module):
        def __init__(self, m, index):
            super().__init__()
            self.m = m
            self.register_buffer("index", index)

        def forward(self, wave):                      # [1, N]
            x = (wave - wave.mean()) / torch.sqrt(wave.var(unbiased=False) + 1e-7)
            lp = torch.log_softmax(self.m(x).logits[0], dim=-1)          # [T, V]
            return lp[:, self.index].amax(dim=-1)                          # [T, 1 + K]

    net = Kana(model, index).eval()
    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / "reazon-kana.onnx"
    x = torch.randn(1, 16000 * 24)
    torch.onnx.export(net, (x,), str(path), input_names=["wave"], output_names=["logp"],
                      dynamic_axes={"wave": {1: "samples"}, "logp": {0: "frames"}}, opset_version=17, dynamo=False)
    json.dump({"model": MODEL, "blank": "column 0", "tokens": tokens, "step": 320 / 16000, "rate": 16000},
              open(OUT / "reazon-kana.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)

    # the tokens in the model itself (orecchiante reads them from its metadata: Words.Tokens)
    import onnx
    m = onnx.load(str(path))
    for k, v in (("tokens", json.dumps(tokens, ensure_ascii=False)), ("source", MODEL), ("license", "Apache-2.0"),
                 ("made_by", "tools/d2/reazon_onnx.py")):
        p = m.metadata_props.add(); p.key = k; p.value = v
    onnx.save(m, str(path))
    import onnxruntime as ort
    from onnxruntime.quantization import quantize_dynamic, QuantType
    variants = {"": path}
    # ConvInteger has no int8-weight kernel in onnxruntime 1.20 (the one .NET runs): Conv only with uint8 weights
    for tag, ops, wt in (("mm8", ["MatMul"], QuantType.QInt8), ("cv8", ["MatMul", "Conv"], QuantType.QUInt8)):
        q = OUT / f"reazon-kana-{tag}.onnx"
        quantize_dynamic(str(path), str(q), weight_type=wt, op_types_to_quantize=ops)
        variants[tag] = q
    y = torch.randn(1, 16000 * 17 + 123)
    with torch.no_grad():
        want = net(y).numpy()
    for tag, p in variants.items():
        s = ort.InferenceSession(str(p), providers=["CPUExecutionProvider"])
        got = s.run(None, {"wave": y.numpy()})[0]
        print(f"{p.name}: {p.stat().st_size / 1e6:.1f} MB, frames {got.shape} vs {want.shape},"
              f" max |diff| {np.abs(got - want).max():.4f}, argmax agree {(got.argmax(1) == want.argmax(1)).mean():.4f}")


if __name__ == "__main__":
    main()

"""Writes the network of Demucs (htdemucs, MIT) to ONNX without its STFT and inverse STFT, which ONNX cannot
do in complex numbers: they are done outside (in .NET, Voice.cs). The network takes one segment of the
mix (2 channels, 7.8 s at 44.1 kHz) and its spectrogram with the real and imaginary parts as channels,
and gives the spectrogram of each source the same way and the time branch's waveform of each source.
Then checks it against torch's forward on a segment of a recording, and writes the copy dettato ships,
htdemucs-mm8.onnx: its matrix products in int8 (85 MB; with the convolutions in int8 too the vocals come out
half as loud, so they are not).

  scratch/sep/venv/Scripts/python tools/d2/demucs_onnx.py <a recording .wav> [--out scratch/d2]
"""
import argparse, math
from pathlib import Path
import numpy as np, soundfile as sf, torch
import torch.nn.functional as F
from demucs.pretrained import get_model
from demucs.hdemucs import pad1d
from demucs.spec import spectro, ispectro


class Core(torch.nn.Module):
    """HTDemucs.forward between _magnitude and _mask (demucs 4.1.0, htdemucs.py), for a whole segment."""

    def __init__(self, m):
        super().__init__()
        self.m = m

    def forward(self, mix, mag):
        m = self.m
        x = mag
        B, C, Fq, T = x.shape
        mean = x.mean(dim=(1, 2, 3), keepdim=True)
        std = x.std(dim=(1, 2, 3), keepdim=True)
        x = (x - mean) / (1e-5 + std)
        xt = mix
        meant = xt.mean(dim=(1, 2), keepdim=True)
        stdt = xt.std(dim=(1, 2), keepdim=True)
        xt = (xt - meant) / (1e-5 + stdt)
        saved, saved_t, lengths, lengths_t = [], [], [], []
        for idx, encode in enumerate(m.encoder):
            lengths.append(x.shape[-1])
            inject = None
            if idx < len(m.tencoder):
                lengths_t.append(xt.shape[-1])
                tenc = m.tencoder[idx]
                xt = tenc(xt)
                if not tenc.empty:
                    saved_t.append(xt)
                else:
                    inject = xt
            x = encode(x, inject)
            if idx == 0 and m.freq_emb is not None:
                frs = torch.arange(x.shape[-2], device=x.device)
                emb = m.freq_emb(frs).t()[None, :, :, None].expand_as(x)
                x = x + m.freq_emb_scale * emb
            saved.append(x)
        if m.crosstransformer:
            if m.bottom_channels:
                b, c, f, t = x.shape
                x = m.channel_upsampler(x.reshape(b, c, f * t))
                x = x.reshape(b, x.shape[1], f, t)
                xt = m.channel_upsampler_t(xt)
            x, xt = m.crosstransformer(x, xt)
            if m.bottom_channels:
                b, c, f, t = x.shape
                x = m.channel_downsampler(x.reshape(b, c, f * t))
                x = x.reshape(b, x.shape[1], f, t)
                xt = m.channel_downsampler_t(xt)
        for idx, decode in enumerate(m.decoder):
            skip = saved.pop(-1)
            x, pre = decode(x, skip, lengths.pop(-1))
            offset = m.depth - len(m.tdecoder)
            if idx >= offset:
                tdec = m.tdecoder[idx - offset]
                length_t = lengths_t.pop(-1)
                if tdec.empty:
                    pre = pre[:, :, 0]
                    xt, _ = tdec(pre, None, length_t)
                else:
                    skip = saved_t.pop(-1)
                    xt, _ = tdec(xt, skip, length_t)
        S = len(m.sources)
        x = x.view(B, S, -1, Fq, T)
        x = x * std[:, None] + mean[:, None]
        xt = xt.view(B, S, -1, mix.shape[-1])
        xt = xt * stdt[:, None] + meant[:, None]
        return x, xt


def spec_in(m, mix):
    """_spec and _magnitude (cac): (1, 2, L) -> (1, 4, 2048, T), and the complex spectrogram."""
    hl = m.hop_length
    le = int(math.ceil(mix.shape[-1] / hl))
    pad = hl // 2 * 3
    x = pad1d(mix, (pad, pad + le * hl - mix.shape[-1]), mode="reflect")
    z = spectro(x, m.nfft, hl)[..., :-1, :][..., 2: 2 + le]
    B, C, Fr, T = z.shape
    return torch.view_as_real(z).permute(0, 1, 4, 2, 3).reshape(B, C * 2, Fr, T), z


def spec_out(m, x, length):
    """_mask (cac) and _ispec: (1, S, 4, 2048, T) -> (1, S, 2, length)."""
    B, S, C, Fr, T = x.shape
    z = torch.view_as_complex(x.view(B, S, -1, 2, Fr, T).permute(0, 1, 2, 4, 5, 3).contiguous())
    hl = m.hop_length
    z = F.pad(F.pad(z, (0, 0, 0, 1)), (2, 2))
    pad = hl // 2 * 3
    le = hl * int(math.ceil(length / hl)) + 2 * pad
    return ispectro(z, hl, length=le)[..., pad: pad + length]


def main():
    p = argparse.ArgumentParser()
    p.add_argument("wav", type=Path)
    p.add_argument("--out", type=Path, default=Path("scratch/d2"))
    p.add_argument("--model", default="htdemucs", help="htdemucs (4 sources) or htdemucs_6s (also guitar and piano)")
    a = p.parse_args()
    m = get_model(a.model).models[0].eval()
    print("sources", m.sources)
    L = int(m.segment * m.samplerate)
    y, sr = sf.read(a.wav, dtype="float32", always_2d=True)
    assert sr == m.samplerate, "give a 44.1 kHz recording"
    if y.shape[1] == 1:
        y = np.repeat(y, 2, axis=1)
    mix = torch.from_numpy(y[sr * 30: sr * 30 + L].T.copy())[None]
    mix = (mix - mix.mean()) / mix.std()
    core = Core(m).eval()
    mag, _ = spec_in(m, mix)
    with torch.no_grad():
        want = m(mix)
        x, xt = core(mix, mag)
        got = xt + spec_out(m, x, L)
    print("the core against forward: max |diff|", float((want - got).abs().max()), "of", float(want.abs().max()))

    a.out.mkdir(parents=True, exist_ok=True)
    path = a.out / f"{a.model}-core.onnx"
    torch.onnx.export(core, (mix, mag), str(path), input_names=["mix", "mag"], output_names=["spec", "wave"],
                      opset_version=17, dynamo=False)
    import onnxruntime as ort
    s = ort.InferenceSession(str(path), providers=["CPUExecutionProvider"])
    ox, oxt = s.run(None, {"mix": mix.numpy(), "mag": mag.numpy()})
    with torch.no_grad():
        og = torch.from_numpy(oxt) + spec_out(m, torch.from_numpy(ox), L)
    voc = m.sources.index("vocals")
    print("onnx against forward: max |diff|", float((want - og).abs().max()), "vocals", float((want[0, voc] - og[0, voc]).abs().max()))
    print(f"{path}: {path.stat().st_size / 1e6:.1f} MB")

    from onnxruntime.quantization import quantize_dynamic, QuantType
    q = a.out / f"{a.model}-mm8.onnx"
    quantize_dynamic(str(path), str(q), weight_type=QuantType.QInt8, op_types_to_quantize=["MatMul"])
    s = ort.InferenceSession(str(q), providers=["CPUExecutionProvider"])
    qx, qxt = s.run(None, {"mix": mix.numpy(), "mag": mag.numpy()})
    with torch.no_grad():
        qg = torch.from_numpy(qxt) + spec_out(m, torch.from_numpy(qx), L)
    rms = lambda v: float(v.pow(2).mean().sqrt())
    for i, name in enumerate(m.sources):
        print(f"int8 against forward: {name} rms {rms(qg[0, i]):.4f} against {rms(want[0, i]):.4f} | diff rms {rms(qg[0, i] - want[0, i]):.4f}")
    print(f"{q}: {q.stat().st_size / 1e6:.1f} MB")


if __name__ == "__main__":
    main()

"""D2: both ears with neither torch nor TensorFlow: numpy, soxr, soundfile, librosa's CQT and onnxruntime.

    scratch/d2/venv-onnx/Scripts/python tools/d2/ears_onnx.py data/synth/seed1 --out scratch/d2/out/seed1

For each .wav: <name>.beats (as tools/track_beats.py writes it) and <name>.npz, <name>.lab (as
tools/crema_chords.py writes them). The mel spectrogram of Beat This! is written out here in numpy (what
a .NET command would have to do); the CQT of crema is librosa's.
"""
import argparse
import json
import time
from pathlib import Path

clock = time.perf_counter()
import numpy as np
import onnxruntime as ort
import soundfile as sf
import soxr
IMPORTED = time.perf_counter() - clock

HERE = Path(__file__).resolve().parents[2] / "scratch" / "d2"      # where crema_onnx.py, bt_onnx.py and crema_classes.py write
ROOTS = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]
QUALITIES = ["min", "maj", "dim", "aug", "min6", "maj6", "min7", "minmaj7", "maj7", "7", "dim7", "hdim7", "sus2", "sus4"]
NAMES = [f"{r}:{q}" for r in ROOTS for q in QUALITIES] + ["X", "N"]


# ---- Beat This!: the log mel spectrogram of beat_this.preprocessing.LogMelSpect, in numpy

def slaney_mel(hz):
    hz = np.asarray(hz, dtype=np.float64)
    return np.where(hz >= 1000.0, 15.0 + np.log(np.maximum(hz, 1e-9) / 1000.0) / (np.log(6.4) / 27.0), hz * 3.0 / 200.0)


def slaney_hz(mel):
    mel = np.asarray(mel, dtype=np.float64)
    return np.where(mel >= 15.0, 1000.0 * np.exp((np.log(6.4) / 27.0) * (mel - 15.0)), mel * 200.0 / 3.0)


def mel_bank(rate=22050, n_fft=1024, low=30.0, high=11000.0, bands=128):
    freqs = np.linspace(0.0, rate // 2, n_fft // 2 + 1)
    points = slaney_hz(np.linspace(slaney_mel(low), slaney_mel(high), bands + 2))
    width = np.diff(points)
    slopes = points[None, :] - freqs[:, None]
    return np.maximum(0.0, np.minimum(-slopes[:, :-2] / width[:-1], slopes[:, 2:] / width[1:])).astype(np.float32)   # (513, 128)


BANK = mel_bank()
WINDOW = (0.5 - 0.5 * np.cos(2.0 * np.pi * np.arange(1024) / 1024)).astype(np.float32)      # hann, periodic


def log_mel(signal):
    """(frames, 128) of a mono signal at 22050 Hz: 1024-sample frames 441 apart, centred, reflected at the ends."""
    padded = np.pad(signal.astype(np.float32), 512, mode="reflect")
    frames = 1 + (len(padded) - 1024) // 441
    at = np.arange(frames)[:, None] * 441 + np.arange(1024)[None, :]
    spectrum = np.abs(np.fft.rfft(padded[at] * WINDOW, axis=1)).astype(np.float32) / np.float32(32.0)   # / sqrt(1024)
    return np.log1p(np.float32(1000.0) * (spectrum @ BANK))


def beat_logits(session, spect, size=1500, border=6):
    """beat_this.inference.split_predict_aggregate (keep_first)."""
    n = len(spect)
    starts = np.arange(-border, n - border, size - 2 * border)
    if n > size - 2 * border:
        starts[-1] = n - (size - border)
    beat, down = np.full(n, -1000.0, dtype=np.float32), np.full(n, -1000.0, dtype=np.float32)
    for start in reversed(list(starts)):
        chunk = spect[max(start, 0): min(start + size, n)]
        chunk = np.pad(chunk, ((max(0, -start), max(0, min(border, start + size - n))), (0, 0)))
        b, d = session.run(None, {"spect": chunk[None]})
        beat[start + border: start + size - border] = b[0][border:-border]
        down[start + border: start + size - border] = d[0][border:-border]
    return beat, down


def peaks(logit, fps=50):
    """beat_this's minimal postprocessing: the largest within 3 frames either side, above 0; neighbours averaged."""
    padded = np.pad(logit, 3, constant_values=-np.inf)
    highest = np.max(np.lib.stride_tricks.sliding_window_view(padded, 7), axis=1)
    found, out = np.nonzero((logit == highest) & (logit > 0))[0], []
    if len(found):
        p, c = int(found[0]), 1
        for q in map(int, found[1:]):
            if q - p <= 1:
                c += 1
                p += (q - p) / c
            else:
                out.append(p)
                p, c = q, 1
        out.append(p)
    return np.array(out) / fps


def beats_of(session, signal, rate, spent, how="soxr"):
    clock = time.perf_counter()
    mono = signal.mean(1) if signal.ndim == 2 else signal
    if rate != 22050:
        if how == "soxr":
            mono = soxr.resample(mono, in_rate=rate, out_rate=22050)
        else:
            from scipy.signal import resample_poly
            from math import gcd
            g = gcd(22050, rate)
            mono = resample_poly(mono, 22050 // g, rate // g)
    spect = log_mel(mono)
    spent["mel"] += time.perf_counter() - clock
    clock = time.perf_counter()
    beat, down = beat_logits(session, spect)
    beats, downs = peaks(beat), peaks(down)
    if len(beats):
        downs = np.unique([beats[np.argmin(np.abs(beats - d))] for d in downs])
    spent["beat model"] += time.perf_counter() - clock
    return beats, downs, spect


def beat_lines(beats, downs):
    lines, number = [], 0
    for t in beats:
        down = len(downs) and float(np.min(np.abs(downs - t))) < 0.035
        number = 1 if down else number + 1 if number else 0
        lines.append(f"{t:.3f}\t{number}")
    return "\n".join(lines) + "\n"


# ---- crema: pumpp's HCQTMag (librosa's CQT of two harmonics, in dB under the loudest), then the model

def hcqt(signal, rate, res_type="soxr_hq"):
    import librosa
    mono = signal.mean(1) if signal.ndim == 2 else signal
    y = mono.astype(np.float32)
    if rate != 44100:
        y = librosa.resample(y, orig_sr=rate, target_sr=44100)
    frames = int(librosa.time_to_frames(librosa.get_duration(y=y, sr=44100), sr=44100, hop_length=4096))
    both = []
    for h in (1, 2):
        c = librosa.cqt(y=y, sr=44100, hop_length=4096, fmin=librosa.note_to_hz("C1") * h, n_bins=216, bins_per_octave=36,
                        res_type=res_type)
        c = librosa.util.fix_length(c, size=frames)
        both.append(librosa.amplitude_to_db(np.abs(c), ref=np.max))
    return np.transpose(np.asarray(both, dtype=np.float32), (2, 1, 0))[None]       # (1, frames, 216, 2)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("input", type=Path)
    p.add_argument("--out", type=Path, required=True)
    p.add_argument("--songs", type=int)
    p.add_argument("--beat", type=Path, default=HERE / "beat-this-final0-dynamo.onnx")
    p.add_argument("--chord", type=Path, default=HERE / "crema-chord.onnx")
    p.add_argument("--resample", default="soxr", help="to 22050 Hz for the mel: soxr or poly (scipy's resample_poly)")
    p.add_argument("--cqt-res", default="soxr_hq", help="librosa.cqt's res_type")
    p.add_argument("--threads", type=int, default=8)
    p.add_argument("--no-beats", action="store_true", help="the chords only")
    p.add_argument("--mel-against", type=Path, help="a folder of <name>.mel.npy from torch, to compare with")
    args = p.parse_args()

    clock = time.perf_counter()
    options = ort.SessionOptions()
    options.intra_op_num_threads = args.threads
    beat_model = ort.InferenceSession(str(args.beat), options, providers=["CPUExecutionProvider"])
    chord_model = ort.InferenceSession(str(args.chord), options, providers=["CPUExecutionProvider"])
    where = json.loads((HERE / "crema-classes.json").read_text())["where"]
    opened = time.perf_counter() - clock
    clock = time.perf_counter()
    import librosa
    librosa.cqt(y=np.zeros(44100, dtype=np.float32), sr=44100, hop_length=4096, n_bins=36, bins_per_octave=36)
    print(f"import numpy, onnxruntime, soundfile, soxr {IMPORTED:.2f} s; open the two models {opened:.2f} s; "
          f"import librosa and a first CQT {time.perf_counter() - clock:.2f} s", flush=True)

    files = sorted(args.input.glob("*.wav")) if args.input.is_dir() else [args.input]
    files = files[: args.songs] if args.songs else files
    args.out.mkdir(parents=True, exist_ok=True)
    spent = {"load": 0.0, "mel": 0.0, "beat model": 0.0, "cqt": 0.0, "chord model": 0.0, "audio": 0.0}
    step, worst_mel = 4096 / 44100, 0.0
    for path in files:
        clock = time.perf_counter()
        signal, rate = sf.read(str(path), dtype="float64")
        spent["load"] += time.perf_counter() - clock
        spent["audio"] += len(signal) / rate
        if not args.no_beats:
            beats, downs, spect = beats_of(beat_model, signal, rate, spent, args.resample)
            if args.mel_against:
                theirs = np.load(args.mel_against / (path.stem + ".mel.npy"))
                worst_mel = max(worst_mel, float(np.abs(theirs - spect).max()) if theirs.shape == spect.shape else np.inf)
            (args.out / (path.stem + ".beats")).write_text(beat_lines(beats, downs), encoding="utf-8", newline="\n")
        clock = time.perf_counter()
        mag = hcqt(signal, rate, args.cqt_res)
        spent["cqt"] += time.perf_counter() - clock
        clock = time.perf_counter()
        prob = chord_model.run(["chord_tag"], {chord_model.get_inputs()[0].name: mag})[0][0]
        logp = np.full_like(prob, -np.inf, dtype=np.float64)
        logp[:, where] = np.log(np.maximum(prob, 1e-12))
        spent["chord model"] += time.perf_counter() - clock
        np.savez_compressed(args.out / (path.stem + ".npz"), logp=logp.astype(np.float16), step=np.float64(step))
        said, lines, start = logp.argmax(-1), [], 0
        for i in range(1, len(said) + 1):
            if i == len(said) or said[i] != said[start]:
                lines.append(f"{start * step:.3f} {i * step:.3f} {NAMES[int(said[start])]}")
                start = i
        (args.out / (path.stem + ".lab")).write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    n = len(files)
    print(f"{n} songs, {spent.pop('audio') / n:.0f} s of audio each; a song: "
          + ", ".join(f"{k} {v / n:.2f} s" for k, v in spent.items()) + f"; in all {sum(spent.values()) / n:.2f} s")
    if args.mel_against:
        print(f"largest gap of the log mel from torch's: {worst_mel:.2e}")


if __name__ == "__main__":
    main()

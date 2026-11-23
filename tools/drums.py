"""The drum part read into a kit's hits on the chart's 16ths and written as a drum staff (2026-11-23): the Python side
the .NET reader follows (src/Orecchiante/Kit.cs). A first try with no truth to score it on (the owner: "wrong is all
right, read them first"); scored only against the generated songs' drums (scratch/drum/try_rule.py), which are not
recordings.

From the drum part Orecchiante separated (<name>.parts/drums.flac):
1. at 22050 Hz, frames of 2048 every 256 samples (librosa.stft's defaults otherwise): the power in three bands, in dB:
   low (under 150 Hz: the bass drum), middle (200 to 3000 Hz: the snare's body), high (6000 Hz and up: the hi-hat and
   the snare's wires); each band's rise = its dB less the lowest of the GAP frames before (0 when it falls).
2. at each 16th of the chart's bars: each band's most rise within 2 frames before to 2 after.
3. each band's own scale: the 90th percentile of its values over 6 dB (its strong hits), at least 12 dB.
   bass drum: the low band at KICK of its scale or more; hi-hat: the high band at HAT dB or more; snare: the middle
   band at SNARE of its scale or more, and either the high band at WIRES of its scale or more or no bass drum there.
4. writing: a drum part in two voices, the hands (snare and hi-hat, stems up) and the feet (bass drum, down); each hit
   lasts to the next hit of its voice within the beat, the voice's rests filling the rest, written as tools/bass.py
   writes lengths.
5. drums heard (else no staff): the drum part's mean square within 30 dB of the recording's (Kit.HeardDb says why).
   .venv/Scripts/python tools/drums.py <drums.flac> <x.orecchiante.json>      (each bar's hands and feet)
"""
import json, sys
from pathlib import Path
import numpy as np

SR, HOP, NFFT = 22050, 256, 2048
LOW, MID, HIGH = 150.0, (200.0, 3000.0), 6000.0
GAP = 3
KICK, HAT, SNARE, WIRES = 0.5, 6.0, 0.7, 0.85
FLOOR, SCALE_MIN = 6.0, 12.0

sys.path.insert(0, str(Path(__file__).parent))
import bass  # noqa: E402  (the grid and the written lengths)


def rises(flac):
    """(3, frames) each band's rise in dB (step 1)."""
    import librosa, soundfile as sf
    y, sr = sf.read(str(flac), dtype="float32", always_2d=True)
    y = librosa.resample(y.mean(axis=1), orig_sr=sr, target_sr=SR)
    S = np.abs(librosa.stft(y, n_fft=NFFT, hop_length=HOP)) ** 2
    f = librosa.fft_frequencies(sr=SR, n_fft=NFFT)
    sel = [f < LOW, (f >= MID[0]) & (f < MID[1]), f >= HIGH]
    db = np.stack([10 * np.log10(S[s].sum(axis=0) + 1e-10) for s in sel])
    r = np.zeros_like(db)
    for t in range(GAP, db.shape[1]):
        r[:, t] = db[:, t] - db[:, t - GAP:t].min(axis=1)
    return np.maximum(r, 0)


def hits(r, slots):
    """Each 16th's drums (step 2, 3): a list of sets of 'bd', 'sn', 'hh'."""
    ft = HOP / SR
    v = np.zeros((len(slots), 3))
    for i, s in enumerate(slots):
        c = int(round(s / ft))
        if 0 <= c - 2 and c + 3 <= r.shape[1]:
            v[i] = r[:, c - 2:c + 3].max(axis=1)
    scale = [max(np.percentile(v[v[:, j] > FLOOR, j], 90) if (v[:, j] > FLOOR).any() else SCALE_MIN, SCALE_MIN) for j in range(3)]
    out = []
    for lo, mi, hi in v:
        kick = lo >= KICK * scale[0]
        h = set()
        if kick:
            h.add("bd")
        if hi >= HAT:
            h.add("hh")
        if mi >= SNARE * scale[1] and (hi >= WIRES * scale[2] or not kick):
            h.add("sn")
        out.append(h)
    return out


def voice_music(starts, n16, names_at):
    """One voice of a bar: hits at 16ths `starts` (each a drum chord's text), each to the next within its beat."""
    toks, pos = [], 0
    for n, a in enumerate(starts):
        if a > pos:
            toks += [f"r{bass.DUR[d]}" for d in bass.pieces(pos, a - pos)]
        end = min(starts[n + 1] if n + 1 < len(starts) else n16, (a // 4 + 1) * 4)
        ds = bass.pieces(a, end - a)
        toks += [f"{names_at[a]}{bass.DUR[d]}" + ("~" if k < len(ds) - 1 else "") for k, d in enumerate(ds)]
        pos = end
    if pos < n16:
        toks += [f"r{bass.DUR[d]}" for d in bass.pieces(pos, n16 - pos)]
    return " ".join(toks)


def chord(names):
    names = sorted(names, key=["sn", "hh"].index) if len(names) > 1 else list(names)
    return names[0] if len(names) == 1 else "<" + " ".join(names) + ">"


def read(flac, side):
    """Each bar's (hands, feet) music."""
    r = rises(flac)
    bars = side["bars"]
    slots, where = bass.slots_of(bars)
    h = hits(r, slots[:-1])
    per = [{} for _ in bars]
    for i, s in enumerate(h):
        k, q = where[i]
        per[k][q] = s
    out = []
    for k, b in enumerate(bars):
        n16 = len(b["beats"]) * 4
        hands = {q: chord(s - {"bd"}) for q, s in per[k].items() if s - {"bd"}}
        feet = {q: "bd" for q, s in per[k].items() if "bd" in s}
        out.append((voice_music(sorted(hands), n16, hands), voice_music(sorted(feet), n16, feet)))
    return out


if __name__ == "__main__":
    side = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))
    for k, (hands, feet) in enumerate(read(sys.argv[1], side)):
        print(k + 1, "|", hands, "||", feet)
